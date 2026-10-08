namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// A write's answer with git write-back ON (<c>gitOutcome()</c>). <see cref="IWriteOutcome.Ok"/>
/// still means the edit is on disk; <see cref="Pushed"/> is what says it reached anywhere that
/// survives the pod restarting, and <see cref="IWriteOutcome.Warning"/> exists so a UI does not
/// have to infer trouble from the absence of something.
/// </summary>
public interface IGitWriteOutcome : IWriteOutcome
{
    bool Pushed { get; }

    /// <summary>Present — as null — when no commit was made.</summary>
    string? Commit { get; }

    /// <summary>Present — as null — when no branch could be chosen.</summary>
    string? Branch { get; }
}
