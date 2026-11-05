using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-USERS-12 for the route tests: the frozen corpora as the clone, plus <c>beta</c>, which lives in
/// its own repository <c>o/beta</c> and is written to its edits branch <c>kit/edits</c> there.
/// </summary>
public static class OwnRepoCorpora
{
    public static readonly ProjectSource Beta = ProjectSource.Parse("o/beta@kit/edits:.kit")!;

    /// <summary>The directory, already read once from (scripted) GitHub.</summary>
    public static async Task<GitHubCorpusDirectory> BuildAsync()
    {
        var d = new GitHubCorpusDirectory(new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot), [Beta], new OneRead());
        await d.RefreshAsync();
        return d;
    }

    private sealed class OneRead : IGitHubCorpusReader
    {
        public Task<ICorpusSnapshot> ReadAsync(IProjectSource source, ICorpusSnapshot? previous = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<ICorpusSnapshot>(new CorpusSnapshot
            {
                Source = source,
                Files = new Dictionary<string, ISnapshotFile>(StringComparer.Ordinal)
                {
                    ["beta.beh"] = new SnapshotFile { Sha = "b1", Text = "behaviour BEH-B \"b\"\n  when opens page:Home\n" },
                },
            });
    }
}
