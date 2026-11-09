using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Client.Domain.DTO;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Events;
using Client.Domain.Parsers;
using Client.Domain.Service;
using Client.Domain.Transports;
using Client.Domain.ValueObjects;
using Client.Infrastructure.Service;

await Tests.Run();

static class Tests
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }

    static Transform At(float x) => new(new Vector3(x, 0, 0), Vector3.Zero, Vector3.Zero, Vector3.Zero);
    static Hero Hero() => new(1, At(0), new FullName("Hero", ""), new VitalStats(100, 100, 0, 0, 0, 0),
        new Phenotype(default, true, default, default), null!, null!, null!, null!, null!, 0, true);
    static NPC Mob(uint id, float x) => new(id, At(x), true, id, default,
        new FullName("Mob", ""), new VitalStats(100, 100, 0, 0, 0, 0));
    static (WorldHandler world, Hero hero, RecordingTransport transport) World()
    {
        var transport = new RecordingTransport();
        var world = new WorldHandler(new MessageBuilder(), transport);
        var hero = Hero();
        world.Handle(new HeroCreatedEvent(hero));
        return (world, hero, transport);
    }

    public static async Task Run()
    {
        var (world, hero, transport) = World();
        var near = Mob(2, 100);
        var far = Mob(3, 200);
        var neutral = Mob(4, 10); neutral.IsHostile = false;
        var dead = Mob(5, 20); dead.VitalStats.Hp = 0;
        var busy = Mob(6, 30); busy.IsInCombatWithOther = true;
        foreach (var npc in new[] { far, near, neutral, dead, busy }) world.Handle(new CreatureCreatedEvent(npc));
        Check(world.FindNearestTarget(150)?.Id == near.Id, "Select nearest live, hostile, unoccupied NPC");
        Check(world.FindNearestTarget(50) == null, "Respect search radius");
        world.Handle(new CreatureDeletedEvent(near.Id));
        Check(!world.IsAvailableTarget(near, 1000), "Deleted targets are invalid");
        hero.VitalStats.Hp = 0;
        Check(world.FindNearestTarget(1000) == null, "Do not select while hero is dead");
        hero.VitalStats.Hp = 100;
        world.Handle(new HeroDeletedEvent());
        Check(world.FindNearestTarget(1000) == null, "No hero means no targets");
        Console.WriteLine("PASS target filters, range, deletion, dead/disconnected hero");

        (world, hero, transport) = World();
        var pathfinder = new FakePathfinder();
        var mover = new AsyncPathMover(world, pathfinder, 2, 0.1, 8, 16);
        Check(!await mover.MoveAsync(new Vector3(500, 0, 0)), "Empty path must not mean arrival");
        Check(transport.Messages.IsEmpty, "Empty path sends no move");
        pathfinder.WithPath = true;
        hero.Transform.Velocity = new Vector3(1, 0, 0);
        Check(!await mover.MoveAsync(new Vector3(500, 0, 0)), "Timeout even if velocity stays nonzero");
        using (var cts = new CancellationTokenSource())
        {
            var moving = mover.MoveAsync(new Vector3(500, 0, 0), cts.Token);
            await Until(() => mover.Path.Count > 0);
            cts.Cancel();
            try { await moving; throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { }
            Check(mover.Path.Count == 0, "Cancellation clears path");
            var count = transport.Messages.Count;
            await Task.Delay(80);
            Check(transport.Messages.Count == count, "No commands after cancellation completes");
        }
        // Superseding a path reports failure for the old request, not successful arrival.
        mover = new AsyncPathMover(world, pathfinder, 1, 2, 8, 16);
        var first = mover.MoveAsync(new Vector3(500, 0, 0));
        await Until(() => mover.Path.Count > 0);
        var second = mover.MoveAsync(new Vector3(600, 0, 0));
        Check(!await first, "Superseded request must return false");
        await Until(() => mover.Path.Count > 0);
        world.Handle(new HeroDeletedEvent());
        Check(!await second, "Disconnect must not count as arrival");
        Console.WriteLine("PASS empty path, node timeout, cancellation, supersession, disconnect");

        (world, hero, transport) = World();
        var nativePathfinder = new BlockingPathfinder();
        mover = new AsyncPathMover(world, nativePathfinder, 1, 1, 8, 16);
        using (var cts = new CancellationTokenSource())
        {
            var pending = mover.MoveAsync(new Vector3(500, 0, 0), cts.Token);
            await Until(() => nativePathfinder.Entered.IsSet);
            cts.Cancel();
            nativePathfinder.Release.Set();
            try { await pending; throw new Exception("Expected cancellation after native search"); }
            catch (OperationCanceledException) { }
            Check(transport.Messages.IsEmpty, "Cancelled native search must not send movement after returning");
        }
        Console.WriteLine("PASS cancellation during native path search");

        (world, hero, transport) = World();
        near = Mob(2, 20); world.Handle(new CreatureCreatedEvent(near));
        var fakeMover = new BlockingMover();
        var engine = new BotEngine(world, fakeMover, 1000, 80);
        engine.Start(); engine.Start();
        await Until(() => engine.State == BotState.Fight);
        Check(fakeMover.Calls == 0, "Already near target: do not move");
        Check(transport.Messages.Count(m => m == "AcquireTarget:2") == 1, "Acquire target once");
        Check(!transport.Messages.Any(m => m.StartsWith("Attack")), "Day 3 never attacks");
        near.Transform.Position = new Vector3(500, 0, 0);
        await Until(() => fakeMover.Calls == 1);
        engine.Stop();
        await Until(() => !engine.IsRunning);
        Check(fakeMover.Cancelled == 1 && engine.State == BotState.Idle, "Stop cancels approach and resets state");
        engine.Start();
        await Until(() => fakeMover.Calls == 2);
        world.Handle(new CreatureDeletedEvent(near.Id));
        await Until(() => fakeMover.Cancelled == 2);
        engine.Stop();
        await Until(() => !engine.IsRunning);
        Console.WriteLine("PASS engine acquisition, hold, moving target, Stop, restart, target deletion");

        // A real approach reaches Fight only after world position confirms arrival.
        near = Mob(3, 500); world.Handle(new CreatureCreatedEvent(near));
        fakeMover = new BlockingMover();
        engine = new BotEngine(world, fakeMover, 1000, 80);
        engine.Start();
        await Until(() => fakeMover.Calls == 1);
        Check(engine.State == BotState.Approach, "Do not enter Fight while still far away");
        hero.Transform.Position = new Vector3(450, 0, 0);
        await Until(() => engine.State == BotState.Fight);
        Check(fakeMover.Cancelled == 1, "Arrival cancels remaining route nodes");
        near.IsInCombatWithOther = true;
        await Until(() => engine.State != BotState.Fight);
        engine.Stop();
        await Until(() => !engine.IsRunning);
        near.IsInCombatWithOther = false;
        hero.Transform.Position = new Vector3(0, 0, 0);
        Console.WriteLine("PASS confirmed arrival, cancel remaining route, occupied target invalidation");

        var throwingMover = new ThrowingMover();
        world.Handle(new CreatureCreatedEvent(near));
        engine = new BotEngine(world, throwingMover, 1000, 80);
        var errors = 0;
        engine.Logged += line => { if (line.StartsWith("Engine error:")) Interlocked.Increment(ref errors); };
        engine.Start();
        await Until(() => Volatile.Read(ref errors) > 0);
        engine.Stop();
        await Until(() => !engine.IsRunning);
        Check(engine.State == BotState.Idle, "Cancellation during error backoff still resets Idle");
        try { _ = new BotEngine(world, fakeMover, double.NaN, 80); throw new Exception("Expected invalid radius rejection"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine("PASS error recovery cleanup, invalid configuration");
    }
}

sealed class MessageBuilder : OutgoingMessageBuilderInterface
{
    public string Build<T>(OutgoingMessage<T> message) => message.Type + ":" + message.Content;
}
sealed class RecordingTransport : TransportInterface
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public event TransportInterface.DelegateMessage? Message { add { } remove { } }
    public bool IsConnected() => true;
    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendAsync(string data, CancellationToken cancellationToken = default)
    { Messages.Enqueue(data); return Task.CompletedTask; }
    public Task StartReceiveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
sealed class FakePathfinder : PathfinderInterface
{
    public bool WithPath;
    public List<PathSegment> FindPath(Vector3 start, Vector3 end) => WithPath
        ? new() { new PathSegment { From = start, To = end } } : new();
}
sealed class BlockingMover : AsyncPathMoverInterface
{
    public ObservableCollection<PathSegment> Path { get; } = new();
    public int Calls;
    public int Cancelled;
    public async Task<bool> MoveAsync(Vector3 location, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        try { await Task.Delay(Timeout.Infinite, cancellationToken); return true; }
        catch (OperationCanceledException) { Interlocked.Increment(ref Cancelled); throw; }
    }
    public async Task MoveUntilReachedAsync(Vector3 location, CancellationToken cancellationToken = default)
        => await MoveAsync(location, cancellationToken);
}
sealed class ThrowingMover : AsyncPathMoverInterface
{
    public ObservableCollection<PathSegment> Path { get; } = new();
    public Task<bool> MoveAsync(Vector3 location, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new InvalidOperationException("Pathfinder unavailable"));
    public async Task MoveUntilReachedAsync(Vector3 location, CancellationToken cancellationToken = default)
        => await MoveAsync(location, cancellationToken);
}

sealed class BlockingPathfinder : PathfinderInterface
{
    public ManualResetEventSlim Entered { get; } = new();
    public ManualResetEventSlim Release { get; } = new();
    public List<PathSegment> FindPath(Vector3 start, Vector3 end)
    {
        Entered.Set();
        if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Native test gate timed out");
        return new() { new PathSegment { From = start, To = end } };
    }
}
