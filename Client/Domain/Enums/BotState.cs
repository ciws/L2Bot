namespace Client.Domain.Enums
{
    /// <summary>
    /// Bot engine state machine states.
    /// Idle -> Target -> Approach -> Fight -> Loot -> Rest -> Idle
    /// </summary>
    public enum BotState
    {
        Idle,
        Target,
        Approach,
        Fight,
        Loot,
        Rest
    }
}
