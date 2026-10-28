using System.Collections.Concurrent;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The in-memory half of hosted coverage (BEH-HEALTH-1): <see cref="RefreshAsync"/> reads the tests
/// repo every corpus names with <c># kit:repo</c>, once per repo however many apps share it, and
/// <see cref="ICoverageStore"/> serves what it found without waiting on GitHub.
/// </summary>
/// <remarks>
/// A failed read keeps the last good reading: yesterday's number with an error beside it says more
/// than a hole. The error is only what the viewer shows when there is no reading at all.
/// </remarks>
public sealed class CoverageStore(ICorpusDirectory corpora, IGitHubTestReader reader, ICoverageCounter counter) : ICoverageStore
{
    private readonly ConcurrentDictionary<string, ITestReading> readings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> errors = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ITestReading? Reading(string repo) => readings.GetValueOrDefault(repo);

    /// <inheritdoc />
    public string? Error(string repo) => errors.GetValueOrDefault(repo);

    /// <summary>Re-read every repo a corpus names. A repo that cannot be read does not stop the others.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var repos = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var app in corpora.Corpora())
        {
            try
            {
                if (RepoDirective.Read(corpora.Read(app)).Repo is { } repo)
                {
                    repos.Add(repo);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or KeyNotFoundException)
            {
                // A corpus a pull removed between the listing and the read names no repo.
            }
        }

        foreach (var repo in repos)
        {
            try
            {
                // Null: the tree has not moved, so the reading we hold is still right.
                if (await reader.ReadTestsAsync(repo, counter, cancellationToken) is { } reading)
                {
                    readings[repo] = reading;
                }

                errors.TryRemove(repo, out _);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                errors[repo] = e is GitHubReadException ? e.Message : $"{e.GetType().Name}: {e.Message}";
            }
        }
    }
}
