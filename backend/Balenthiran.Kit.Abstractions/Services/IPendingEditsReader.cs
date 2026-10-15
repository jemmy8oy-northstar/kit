using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Reads what the edits branch holds that the base does not (BEH-COMMIT-5, kit#118's "view in kit
/// the pending updates before they are merged"). Read-only: it asks GitHub and changes nothing.
/// </summary>
public interface IPendingEditsReader
{
    /// <summary>
    /// The commits on <paramref name="head"/> that <paramref name="baseBranch"/> lacks, newest
    /// first, and the open pull request between them. Never throws for a GitHub or network
    /// failure: the result carries a reason instead.
    /// </summary>
    Task<IPendingEdits> PendingAsync(string head, string baseBranch, CancellationToken cancellationToken = default);
}
