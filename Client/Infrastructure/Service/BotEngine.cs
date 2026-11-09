using Client.Domain.Enums;
using Client.Domain.Entities;
using Client.Domain.ValueObjects;
using Client.Domain.Service;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Infrastructure.Service
{
    /// <summary>Autonomous target selection and approach; combat is added in the next roadmap step.</summary>
    public class BotEngine : BotEngineInterface
    {
        private readonly WorldHandler worldHandler;
        private readonly AsyncPathMoverInterface pathMover;

        private readonly double searchRadius;
        private readonly double approachDistance;
        private NPC? target;
        private Hero? sessionHero;

        private CancellationTokenSource? cancellationTokenSource;
        private Task? loopTask;

        public BotState State { get; private set; } = BotState.Idle;
        public bool IsRunning => loopTask != null && !loopTask.IsCompleted;

        public event Action<BotState>? StateChanged;
        public event Action<string>? Logged;

        public BotEngine(WorldHandler worldHandler, AsyncPathMoverInterface pathMover, double searchRadius, double approachDistance)
        {
            this.worldHandler = worldHandler;
            this.pathMover = pathMover;
            if (!double.IsFinite(searchRadius) || searchRadius <= 0)
                throw new ArgumentOutOfRangeException(nameof(searchRadius), "Target search radius must be positive.");
            if (!double.IsFinite(approachDistance) || approachDistance <= 0 || approachDistance > searchRadius)
                throw new ArgumentOutOfRangeException(nameof(approachDistance), "Approach distance must be positive and within the search radius.");
            this.searchRadius = searchRadius;
            this.approachDistance = approachDistance;
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            loopTask = Task.Run(() => RunAsync(token));
            Log("Bot engine started");
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            cancellationTokenSource?.Cancel();
            Log("Bot engine stopping...");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var nextState = State switch
                        {
                            BotState.Idle => await HandleIdleAsync(cancellationToken),
                            BotState.Target => await HandleTargetAsync(cancellationToken),
                            BotState.Approach => await HandleApproachAsync(cancellationToken),
                            BotState.Fight => await HandleFightAsync(cancellationToken),
                            BotState.Loot => await HandleLootAsync(cancellationToken),
                            BotState.Rest => await HandleRestAsync(cancellationToken),
                            _ => BotState.Idle
                        };
                        cancellationToken.ThrowIfCancellationRequested();
                        if (nextState != State) SetState(nextState);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log("Engine error: " + ex.Message);
                        target = null;
                        SetState(BotState.Idle);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                target = null;
                sessionHero = null;
                SetState(BotState.Idle);
                Log("Bot engine stopped");
            }
        }

        private void SetState(BotState newState)
        {
            Log("State: " + State + " -> " + newState);
            State = newState;
            StateChanged?.Invoke(newState);
        }

        private void Log(string message)
        {
            Logged?.Invoke(message);
        }

        private async Task<BotState> HandleIdleAsync(CancellationToken cancellationToken)
        {
            // TODO day 6: check rest conditions (HP/MP thresholds) -> Rest
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Target;
        }

        private async Task<BotState> HandleTargetAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(StateTickDelayMs, cancellationToken);
            sessionHero = worldHandler.Hero;
            target = worldHandler.FindNearestTarget(searchRadius);
            if (target == null) return BotState.Idle;

            cancellationToken.ThrowIfCancellationRequested();
            worldHandler.RequestAcquireTarget(target.Id);
            Log($"Target: {target.Name} ({target.Id}), distance {DistanceToTarget():F0}");
            return BotState.Approach;
        }

        private bool HasValidTarget() => target != null && ReferenceEquals(sessionHero, worldHandler.Hero)
            && worldHandler.IsAvailableTarget(target, searchRadius);

        private double DistanceToTarget() => sessionHero!.Transform.Position.HorizontalDistance(target!.Transform.Position);

        private bool IsWithinApproachDistance() => DistanceToTarget() <= approachDistance
            && Math.Abs(sessionHero!.Transform.Position.Z - target!.Transform.Position.Z) <= approachDistance;

        private async Task<BotState> HandleApproachAsync(CancellationToken cancellationToken)
        {
            if (!HasValidTarget()) return BotState.Target;
            if (IsWithinApproachDistance())
            {
                Log("Target reached; waiting for combat (day 4)");
                return BotState.Fight;
            }

            var position = target!.Transform.Position;
            var destination = new Vector3(position.X, position.Y, position.Z);
            using var movement = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var moveTask = pathMover.MoveAsync(destination, movement.Token);
            var repath = false;
            try
            {
                while (!moveTask.IsCompleted)
                {
                    await Task.WhenAny(moveTask, Task.Delay(100, cancellationToken));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!HasValidTarget() || IsWithinApproachDistance()) break;
                    if (target.Transform.Position.HorizontalDistance(destination) > approachDistance)
                    {
                        repath = true;
                        break;
                    }
                }
            }
            finally
            {
                movement.Cancel();
                try { await moveTask; }
                catch (OperationCanceledException) when (movement.IsCancellationRequested) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasValidTarget())
            {
                Log("Target lost or no longer available");
                return BotState.Target;
            }
            if (IsWithinApproachDistance())
            {
                Log("Target reached; waiting for combat (day 4)");
                return BotState.Fight;
            }
            if (repath) return BotState.Approach;

            Log("Target not reached; searching again");
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Target;
        }

        private async Task<BotState> HandleFightAsync(CancellationToken cancellationToken)
        {
            // Day 3 holds the target without sending attacks. Day 4 adds combat.
            await Task.Delay(StateTickDelayMs, cancellationToken);
            if (!HasValidTarget()) return BotState.Target;
            return IsWithinApproachDistance() ? BotState.Fight : BotState.Approach;
        }

        private async Task<BotState> HandleLootAsync(CancellationToken cancellationToken)
        {
            // TODO day 5: pick up drop within radius
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Idle;
        }

        private async Task<BotState> HandleRestAsync(CancellationToken cancellationToken)
        {
            // TODO day 6: sit until HP/MP recovered, potions
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Idle;
        }

        private const int StateTickDelayMs = 500;
    }
}
