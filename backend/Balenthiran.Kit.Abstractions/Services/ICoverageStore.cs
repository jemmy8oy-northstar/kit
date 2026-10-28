using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// What hosted Kit last read of each app's tests repository (BEH-HEALTH-1), kept in memory so a
/// request never waits on GitHub. Absent altogether in local mode, where there is no repo to read.
/// </summary>
public interface ICoverageStore
{
    /// <summary>The last good reading of <paramref name="repo"/> (<c>owner/repo@branch</c>), or null if none has landed yet.</summary>
    ITestReading? Reading(string repo);

    /// <summary>Why the last read of the repo failed, or null. A failure never discards <see cref="Reading"/>.</summary>
    string? Error(string repo);
}
