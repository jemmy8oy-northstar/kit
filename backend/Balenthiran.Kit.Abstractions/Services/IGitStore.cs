using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Puts a corpus edit back into git, for a Kit that is not running on a laptop
/// (<c>git-store.js</c>, kit#41/#43). Off by default: locally a write lands in the working
/// tree and the author reviews the diff (<c>docs/design/ui.md</c> decision 2). Deployed,
/// a pod's disk is wiped on restart, so the edit only survives if it is committed and pushed.
/// </summary>
public interface IGitStore
{
    /// <summary>Whether write-back is switched on (<c>ui.js</c>'s <c>--git</c>).</summary>
    bool Enabled { get; }

    /// <summary>The remote a push goes to, as the answer's warning names it: <c>origin</c> unless set.</summary>
    string Remote { get; }

    /// <summary>
    /// Commit <paramref name="file"/> — which the writer has ALREADY written — and push it.
    /// Never throws for a git failure: every outcome that is not a clean push carries a
    /// reason, and committed and pushed are separate facts, because a stranded commit
    /// reported as success is how a deployed Kit loses work silently.
    /// </summary>
    /// <param name="summary">What the edit was, for the commit message (<c>add BEH-7</c>).</param>
    /// <param name="app">The corpus, named in the commit message.</param>
    IGitWriteBack WriteBack(string file, string summary, string? app);
}
