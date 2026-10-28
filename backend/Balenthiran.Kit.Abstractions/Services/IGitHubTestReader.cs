using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Reads every test title in one repository branch through GitHub's REST API (BEH-HEALTH-1): one
/// recursive tree, then the blob of each test file whose sha moved since the last read. A blob is
/// fetched by sha, so the text is the text at that tree even if the branch moves mid-read.
/// </summary>
public interface IGitHubTestReader
{
    /// <summary>
    /// The titles of <paramref name="repo"/> (<c>owner/repo@branch</c>) now, or null when the tree
    /// has not changed since the last successful read of it. Throws
    /// <see cref="Exceptions.GitHubReadException"/> rather than answer with fewer tests than exist.
    /// </summary>
    Task<ITestReading?> ReadTestsAsync(string repo, ICoverageCounter counter, CancellationToken cancellationToken = default);
}
