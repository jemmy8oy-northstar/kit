using System.Collections.Concurrent;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The body of <c>GET /api/projects/{app}/history</c>: <see cref="Completions.Of"/> over the corpus
/// as served and its git history.
/// </summary>
/// <remarks>
/// A commit never changes, so each version is scanned once and kept: after the first board load a
/// request costs one <c>git log</c> and a <c>git show</c> per commit since. The server is
/// concurrent, and a scan computed twice by two racing requests is the same scan.
/// </remarks>
public sealed class CompletionReader(ICorpusDirectory corpora, ICorpusHistory history) : ICompletionReader
{
    private readonly ConcurrentDictionary<(string File, string Commit), IReadOnlyList<KeyValuePair<string, bool>>> scans = new();

    /// <inheritdoc />
    public ICompletionList For(string app)
    {
        var file = corpora.FullPath(app);
        var log = history.Log(file);
        if (log.Reason is { } reason)
        {
            return new CompletionList { App = app, File = corpora.RelativePath(app), History = "none", Reason = reason, Completed = [] };
        }

        var versions = log.Commits
            .Select(c => (ICorpusVersion)new CorpusVersion { At = c.At, Pending = Version(file, c.Commit) })
            .ToList();

        return new CompletionList
        {
            App = app,
            File = corpora.RelativePath(app),
            History = "git",
            Reason = null,
            Completed = Completions.Of(Completions.Scan(corpora.ReadText(app)), versions),
        };
    }

    private IReadOnlyList<KeyValuePair<string, bool>> Version(string file, string commit)
    {
        if (scans.TryGetValue((file, commit), out var kept)) return kept;

        // No text is a commit that deleted the file — nothing was in it then — or a git failure.
        // Only a read is kept: a failure cached as "empty" would misdate every behaviour around
        // that commit until the server restarted.
        if (history.Text(file, commit) is not { } text) return [];
        return scans.GetOrAdd((file, commit), Completions.Scan(text));
    }
}
