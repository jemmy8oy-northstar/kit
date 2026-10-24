namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One message from the agent, as the notifications page shows it (BEH-NOTIFY-1).</summary>
public interface INotification
{
    /// <summary>ISO 8601 UTC, as the agent wrote it.</summary>
    string At { get; }

    /// <summary>wake, sleep, pr, blocker, done or note; anything else reads as note.</summary>
    string Kind { get; }

    /// <summary>The message, shown as plain text.</summary>
    string Text { get; }
}
