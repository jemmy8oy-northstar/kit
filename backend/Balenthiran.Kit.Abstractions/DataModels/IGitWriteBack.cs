namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What git did with one edit (<c>writeBack()</c>'s return). By the time this exists the
/// file is already on disk, so a failure never means "the edit was lost" — it means "the
/// edit is on a disk nobody will read again", and <see cref="Committed"/> and
/// <see cref="Pushed"/> are kept apart so the two cannot be confused.
/// </summary>
public interface IGitWriteBack
{
    bool Committed { get; }

    bool Pushed { get; }

    /// <summary>Set on every outcome that is not a clean push, the benign ones included — git's own words where git spoke.</summary>
    string? Reason { get; }

    /// <summary>The commit's first ten hex digits, once one exists.</summary>
    string? Commit { get; }

    /// <summary>The branch pushed to, or null when none could be chosen.</summary>
    string? Branch { get; }
}
