using Client.Domain.Enums;
using Client.Domain.Entities;
using Client.Domain.ValueObjects;
using Client.Domain.Service;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Client.Infrastructure.Service
{
    /// <summary>Autonomous targeting, approach and basic attacks with bounded retries.</summary>
    public class BotEngine : BotEngineInterface
    {
        private readonly WorldHandler worldHandler;
        private readonly AsyncPathMoverInterface pathMover;

        private readonly double searchRadius;
        private readonly double approachDistance;
        private NPC? target;
        private Hero? sessionHero;
        private readonly double attackInterval, approachTimeout, combatTimeout, targetCooldown;
        private readonly Dictionary<uint, long> excludedUntil = new();
        private long approachStarted, lastDamage, lastAttack;
        private uint lowestHp;
        private bool attacking;

        private static double Elapsed(long start) => Stopwatch.GetElapsedTime(start).TotalSeconds;

        private CancellationTokenSource? cancellationTokenSource;
        private Task? loopTask;

        public BotState State { get; private set; } = BotState.Idle;
        public bool IsRunning => loopTask != null && !loopTask.IsCompleted;

        public event Action<BotState>? StateChanged;
        public event Action<string>? Logged;

        public BotEngine(WorldHandler worldHandler, AsyncPathMoverInterface pathMover, double searchRadius, double approachDistance,
            double attackInterval = 1, double approachTimeout = 15, double combatTimeout = 20, double targetCooldown = 30)
        {
            this.worldHandler = worldHandler;
            this.pathMover = pathMover;
            if (!double.IsFinite(searchRadius) || searchRadius <= 0)
                throw new ArgumentOutOfRangeException(nameof(searchRadius), "Target search radius must be positive.");
            if (!double.IsFinite(approachDistance) || approachDistance <= 0 || approachDistance > searchRadius)
                throw new ArgumentOutOfRangeException(nameof(approachDistance), "Approach distance must be positive and within the search radius.");
            this.searchRadius = searchRadius;
            this.approachDistance = approachDistance;
            foreach (var (name, value) in new[] { (nameof(attackInterval), attackInterval), (nameof(approachTimeout), approachTimeout),
                (nameof(combatTimeout), combatTimeout), (nameof(targetCooldown), targetCooldown) })
                if (!double.IsFinite(value) || value <= 0 || value > TimeSpan.FromDays(1).TotalSeconds)
                    throw new ArgumentOutOfRangeException(name, "Combat intervals must be positive, finite, and at most one day.");
            this.attackInterval = attackInterval;
            this.approachTimeout = approachTimeout;
            this.combatTimeout = combatTimeout;
            this.targetCooldown = targetCooldown;
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
                        if (sessionHero != null && (!worldHandler.IsConnected || !ReferenceEquals(sessionHero, worldHandler.Hero) || sessionHero.VitalStats.IsDead))
                        {
                            Log("Hero died or session disconnected; stopping engine");
                            break;
                        }
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
                        await HaltAttackAsync();
                        target = null;
                        SetState(BotState.Idle);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                try { await HaltAttackAsync(); }
                catch (Exception ex) { Log("Halt failed: " + ex.Message); }
                target = null;
                sessionHero = null;
                excludedUntil.Clear();
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
            await HaltAttackAsync();
            await Task.Delay(StateTickDelayMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!worldHandler.IsConnected) return BotState.Idle;
            sessionHero = worldHandler.Hero;
            foreach (var id in excludedUntil.Where(p => Stopwatch.GetTimestamp() >= p.Value).Select(p => p.Key).ToArray()) excludedUntil.Remove(id);
            target = worldHandler.FindNearestTarget(searchRadius, npc => !excludedUntil.ContainsKey(npc.Id));
            if (target == null) return BotState.Idle;

            approachStarted = 0;
            lastDamage = 0;
            lastAttack = 0;

            cancellationToken.ThrowIfCancellationRequested();
            await worldHandler.RequestBotAcquireAsync(sessionHero!, target, searchRadius, cancellationToken);
            Log($"Target: {target.Name} ({target.Id}), distance {DistanceToTarget():F0}");
            return BotState.Approach;
        }

        private bool HasValidTarget() => worldHandler.IsConnected && target != null && ReferenceEquals(sessionHero, worldHandler.Hero)
            && worldHandler.IsAvailableTarget(target, searchRadius);

        private async Task HaltAttackAsync()
        {
            if (!attacking) return;
            attacking = false;
            if (sessionHero != null) await worldHandler.RequestBotHaltAsync(sessionHero);
        }

        private async Task<BotState> SkipTargetAsync(string reason)
        {
            if (target != null)
            {
                excludedUntil[target.Id] = Stopwatch.GetTimestamp() + (long)(targetCooldown * Stopwatch.Frequency);
                Log($"Skipping target {target.Id} for {targetCooldown:F0}s: {reason}");
            }
            await HaltAttackAsync();
            return BotState.Target;
        }

        private double DistanceToTarget() => sessionHero!.Transform.Position.HorizontalDistance(target!.Transform.Position);

        private bool IsWithinApproachDistance() => DistanceToTarget() <= approachDistance
            && Math.Abs(sessionHero!.Transform.Position.Z - target!.Transform.Position.Z) <= approachDistance;

        private async Task<BotState> HandleApproachAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasValidTarget()) return BotState.Target;
            if (approachStarted == 0) approachStarted = Stopwatch.GetTimestamp();
            if (Elapsed(approachStarted) >= approachTimeout) return await SkipTargetAsync("approach timeout");
            if (IsWithinApproachDistance())
            {
                approachStarted = 0;
                Log("Target reached; starting combat");
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
                    if (Elapsed(approachStarted) >= approachTimeout) break;
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
                approachStarted = 0;
                Log("Target reached; starting combat");
                return BotState.Fight;
            }
            if (Elapsed(approachStarted) >= approachTimeout) return await SkipTargetAsync("approach timeout");
            if (repath) return BotState.Approach;

            return await SkipTargetAsync("route failed");
        }

        private async Task<BotState> HandleFightAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(StateTickDelayMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!worldHandler.IsConnected || sessionHero == null || !ReferenceEquals(sessionHero, worldHandler.Hero) || sessionHero.VitalStats.IsDead)
                return BotState.Target;
            if (target != null && worldHandler.ContainsTarget(target) && target.VitalStats.IsDead)
            {
                Log($"Target {target.Id} died");
                await HaltAttackAsync();
                return BotState.Loot;
            }
            if (!HasValidTarget()) return BotState.Target;
            if (lastDamage == 0 || target!.VitalStats.Hp < lowestHp)
            {
                lowestHp = target!.VitalStats.Hp;
                lastDamage = Stopwatch.GetTimestamp();
            }
            if (Elapsed(lastDamage) >= combatTimeout) return await SkipTargetAsync("no damage progress");
            if (!IsWithinApproachDistance())
            {
                await HaltAttackAsync();
                return BotState.Approach;
            }
            if (lastAttack == 0 || Elapsed(lastAttack) >= attackInterval)
            {
                attacking = true;
                await worldHandler.RequestBotAttackAsync(sessionHero, target!, searchRadius, cancellationToken);
                lastAttack = Stopwatch.GetTimestamp();
            }
            return BotState.Fight;
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
