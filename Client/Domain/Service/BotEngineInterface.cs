using Client.Domain.Enums;
using System;

namespace Client.Domain.Service
{
    /// <summary>
    /// Autonomous bot engine. Runs a state machine loop when started:
    /// Idle -> Target -> Approach -> Fight -> Loot -> Rest -> Idle
    /// </summary>
    public interface BotEngineInterface
    {
        BotState State { get; }
        bool IsRunning { get; }

        /// <summary>Raised on any state transition. May be called from a background thread.</summary>
        event Action<BotState>? StateChanged;

        /// <summary>Raised on engine log lines. May be called from a background thread.</summary>
        event Action<string>? Logged;

        void Start();
        void Stop();
    }
}
