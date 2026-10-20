using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// A corpus file's past (BEH-BOARD-4). Read from the git work tree the file sits in — on a laptop
/// the developer's checkout, deployed the pod's clone of the branch it serves — so it asks nothing
/// of GitHub.
/// </summary>
public interface ICorpusHistory
{
    /// <summary>
    /// The commits that changed <paramref name="file"/> on the checked-out branch's first-parent line,
    /// oldest first. Never throws: a file outside a work tree, or git that cannot run, is a
    /// <see cref="ICommitLog.Reason"/>.
    /// </summary>
    ICommitLog Log(string file);

    /// <summary>
    /// The newest <paramref name="limit"/> commits that changed <paramref name="file"/> or its
    /// <paramref name="notes"/> (BEH-ACTIVITY-1), newest first, each with its subject. Both sit in one
    /// directory. Never throws: a corpus not on this server's disk, outside a work tree, or git that
    /// cannot run is a <see cref="ICommitHeadlines.Reason"/>.
    /// </summary>
    ICommitHeadlines Recent(string file, string notes, int limit);

    /// <summary>The file's text at <paramref name="commit"/>, or null when git could not give it.</summary>
    string? Text(string file, string commit);
}
