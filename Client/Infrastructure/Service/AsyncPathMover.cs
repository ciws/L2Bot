using Client.Domain.DTO;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Infrastructure.Service
{
    public class AsyncPathMover : AsyncPathMoverInterface
    {
        private readonly WorldHandler worldHandler;
        private readonly PathfinderInterface pathfinder;
        private readonly int pathNumberOfAttempts;
        private readonly double nodeWaitingTime;
        private readonly int nodeDistanceTolerance;
        private readonly int nextNodeDistanceTolerance;
        private readonly object movementLock = new object();
        private readonly SemaphoreSlim movementGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? activeMovement;

        public ObservableCollection<PathSegment> Path { get; } = new ObservableCollection<PathSegment>();

        public async Task MoveUntilReachedAsync(Vector3 location, CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < Math.Max(1, pathNumberOfAttempts); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await MoveCoreAsync(location, cancellationToken);
                if (result.Reached || result.Superseded)
                {
                    return;
                }
            }
        }

        public async Task<bool> MoveAsync(Vector3 location, CancellationToken cancellationToken = default)
            => (await MoveCoreAsync(location, cancellationToken)).Reached;

        private async Task<(bool Reached, bool Superseded)> MoveCoreAsync(Vector3 location, CancellationToken cancellationToken)
        {
            using var movement = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (movementLock)
            {
                activeMovement?.Cancel();
                activeMovement = movement;
            }

            var entered = false;
            var sentMove = false;
            var arrived = false;
            var hero = worldHandler.Hero;
            try
            {
                await movementGate.WaitAsync(movement.Token);
                entered = true;
                arrived = await Task.Run(async () =>
                {
                    var token = movement.Token;
                    token.ThrowIfCancellationRequested();
                    Path.Clear();
                    if (hero == null || hero.VitalStats.IsDead || !ReferenceEquals(hero, worldHandler.Hero))
                    {
                        return false;
                    }
                    if (hero.Transform.Position.ApproximatelyEquals(location, nodeDistanceTolerance, withZ: true))
                    {
                        return true;
                    }

                    // Native pathfinding cannot be interrupted; check cancellation before sending commands.
                    var path = pathfinder.FindPath(hero.Transform.Position, location);
                    token.ThrowIfCancellationRequested();
                    if (path.Count == 0)
                    {
                        return false;
                    }
                    foreach (var segment in path) Path.Add(segment);

                    foreach (var node in path)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!ReferenceEquals(hero, worldHandler.Hero) || hero.VitalStats.IsDead) return false;
                        sentMove = true;
                        worldHandler.RequestMoveToLocation(node.To);
                        var timer = Stopwatch.StartNew();
                        while (!hero.Transform.Position.ApproximatelyEquals(node.To, nodeDistanceTolerance, withZ: true))
                        {
                            token.ThrowIfCancellationRequested();
                            if (!ReferenceEquals(hero, worldHandler.Hero) || hero.VitalStats.IsDead) return false;
                            if (hero.Transform.Velocity.Equals(Vector3.Zero)
                                && hero.Transform.Position.ApproximatelyEquals(node.To, nextNodeDistanceTolerance, withZ: true)) break;
                            if (timer.Elapsed.TotalSeconds >= nodeWaitingTime) return false;
                            await Task.Delay(25, token);
                        }
                        Path.Remove(node);
                    }
                    return true;
                }, movement.Token);
                return (arrived, false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A newer movement request superseded this one; cancellation is not arrival.
                return (false, true);
            }
            finally
            {
                if (entered)
                {
                    if (sentMove && (!arrived || movement.IsCancellationRequested) && hero != null
                        && ReferenceEquals(hero, worldHandler.Hero) && !hero.VitalStats.IsDead)
                    {
                        worldHandler.RequestMoveToLocation(hero.Transform.Position);
                    }
                    Path.Clear();
                    movementGate.Release();
                }
                lock (movementLock)
                {
                    if (ReferenceEquals(activeMovement, movement)) activeMovement = null;
                }
            }
        }

        public AsyncPathMover(WorldHandler worldHandler, PathfinderInterface pathfinder, int pathNumberOfAttempts,
            double nodeWaitingTime, int nodeDistanceTolerance, int nextNodeDistanceTolerance)
        {
            this.worldHandler = worldHandler;
            this.pathfinder = pathfinder;
            this.pathNumberOfAttempts = pathNumberOfAttempts;
            this.nodeWaitingTime = nodeWaitingTime;
            this.nodeDistanceTolerance = nodeDistanceTolerance;
            this.nextNodeDistanceTolerance = nextNodeDistanceTolerance;
        }
    }
}
