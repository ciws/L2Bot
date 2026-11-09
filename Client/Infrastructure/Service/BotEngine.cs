using Client.Domain.Enums;
using Client.Domain.Service;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Infrastructure.Service
{
    /// <summary>
    /// State machine driven bot engine.
    /// Day 2 skeleton: states and transitions are wired, state handlers are stubs
    /// (filled in by later roadmap steps: targeting, combat, looting, rest).
    /// The skeleton never sends game commands - it only logs transitions.
    /// </summary>
    public class BotEngine : BotEngineInterface
    {
        private readonly WorldHandler worldHandler;
        private readonly AsyncPathMoverInterface pathMover;

        private CancellationTokenSource? cancellationTokenSource;
        private Task? loopTask;

        public BotState State { get; private set; } = BotState.Idle;
        public bool IsRunning => loopTask != null && !loopTask.IsCompleted;

        public event Action<BotState>? StateChanged;
        public event Action<string>? Logged;

        public BotEngine(WorldHandler worldHandler, AsyncPathMoverInterface pathMover)
        {
            this.worldHandler = worldHandler;
            this.pathMover = pathMover;
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            loopTask = Task.Run(() => RunAsync(cancellationTokenSource.Token));
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

                    if (nextState != State)
                    {
                        SetState(nextState);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log("Engine error: " + ex.Message);
                    await Task.Delay(1000, cancellationToken);
                }
            }

            SetState(BotState.Idle);
            Log("Bot engine stopped");
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
            // TODO day 3: pick nearest attackable mob within radius via worldHandler
            // No target found yet (stub) -> back to Idle
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Idle;
        }

        private async Task<BotState> HandleApproachAsync(CancellationToken cancellationToken)
        {
            // TODO day 3: move to target via pathMover.MoveAsync
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Idle;
        }

        private async Task<BotState> HandleFightAsync(CancellationToken cancellationToken)
        {
            // TODO day 4: attack loop until target death, anti-stuck timeout
            await Task.Delay(StateTickDelayMs, cancellationToken);
            return BotState.Idle;
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
