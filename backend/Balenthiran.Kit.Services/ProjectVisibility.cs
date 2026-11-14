using System.Collections.Concurrent;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The in-memory half of BEH-GATE-H1 (kit#795): <see cref="RefreshAsync"/> asks GitHub whether
/// each repo a corpus names with <c># kit:repo</c> is public, once per repo however many apps share
/// it, and <see cref="IProjectVisibility"/> answers from what it found without waiting on GitHub.
/// </summary>
/// <remarks>
/// Fails closed, unlike <see cref="CoverageStore"/>: a read that fails forgets the repo, so a repo
/// made private is locked on the next read whatever the last answer was. A visitor who loses a page
/// for one poll costs less than a spec served for one poll after it should have stopped.
/// </remarks>
public sealed class ProjectVisibility(ICorpusDirectory corpora, IGitHubRepoReader reader) : IProjectVisibility
{
    private volatile IReadOnlySet<string> open = new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsPublic(string app) => open.Contains(app);

    /// <summary>Re-ask about every repo a corpus names, then swap the answer in whole.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var repoOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var app in corpora.Corpora())
        {
            try
            {
                if (RepoDirective.Read(corpora.Read(app)).Repo is { } repo)
                {
                    repoOf[app] = repo[..repo.IndexOf('@', StringComparison.Ordinal)];
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or KeyNotFoundException)
            {
                // A corpus a pull removed between the listing and the read names no repo, so it is not public.
            }
        }

        var answers = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var repo in repoOf.Values.Distinct(StringComparer.Ordinal))
        {
            try
            {
                answers[repo] = await reader.IsPublicAsync(repo, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                answers[repo] = false;
                await Console.Error.WriteLineAsync($"kit: could not tell whether {repo} is public, so its projects need a sign-in — {e.Message}");
            }
        }

        open = repoOf.Where(p => answers[p.Value]).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    }
}
