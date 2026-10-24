namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// When the agent next wakes (BEH-NOTIFY-5), as its scheduler's own pause file says: the
/// <c>state/suspend.json</c> its gate reads before every session. Kit shows it; the page turns it
/// into words, because whether an <see cref="Until"/> has passed depends on when it is read.
/// </summary>
public interface IAgentSchedule
{
    /// <summary>True while the gate is skipping sessions.</summary>
    bool Suspended { get; }

    /// <summary>ISO 8601 UTC the pause ends; null for a pause with no end, or when not suspended.</summary>
    string? Until { get; }

    /// <summary>
    /// The agent paused itself (<c>set_by: bot</c>), so his next action in Kit also wakes it
    /// (BEH-NOTIFY-4). A pause he set is never cut short by Kit.
    /// </summary>
    bool ByAgent { get; }

    /// <summary>The agent's own reason for the pause, shown as plain text; null when none.</summary>
    string? Why { get; }

    /// <summary>
    /// Why Kit cannot say, or that no schedule is configured. Null when the file was read. A
    /// schedule Kit cannot read is never shown as an awake agent.
    /// </summary>
    string? Reason { get; }
}
