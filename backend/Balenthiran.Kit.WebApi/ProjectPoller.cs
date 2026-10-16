using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// Re-reads the <c>KIT_PROJECTS</c> sources every <see cref="KitSettings.ProjectsPoll"/> (kit#88).
/// The first read runs at start-up, in the background: until it lands the clone answers, so a slow
/// or absent GitHub delays fresh projects rather than the server. A failed read is said on stderr
/// and the last good snapshot keeps serving.
///
/// Before each read it brings the edits branch up to date with dev (kit#300, BEH-COMMIT-6), so a
/// spec merged into dev is on the branch the read is about to fetch. What that did is said on
/// stderr only when it differs from last time: "already up to date" every two minutes is noise,
/// and a conflict said once, then again only when it clears or changes, is still said.
/// </summary>
public sealed class ProjectPoller(GitHubCorpusDirectory directory, KitSettings settings, TimeProvider clock, IKitRouter router) : BackgroundService
{
    private string? lastCatchUp;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(settings.ProjectsPoll, clock);
        do
        {
            await CatchUpAsync();
            try
            {
                await directory.RefreshAsync(stoppingToken);
            }
            catch (GitHubReadException e)
            {
                var serving = directory.FromGitHub ? "the last snapshot" : "the clone";
                await Console.Error.WriteLineAsync($"kit: could not read KIT_PROJECTS — {e.Message}; serving {serving}");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CatchUpAsync()
    {
        string said;
        try
        {
            var r = router.CatchUp(settings.Dir);
            said = r.Committed
                ? $"kit: merged {settings.GitBase} into {r.Branch} ({r.Commit}){(r.Pushed ? " and pushed it" : $", not pushed: {r.Reason}")}"
                : $"kit: did not catch up with {settings.GitBase} — {r.Reason}";
        }
        catch (Exception e)
        {
            // A background loop that throws stops polling for good; the read below must still run.
            said = $"kit: catching up with {settings.GitBase} failed — {e.Message}";
        }

        if (said != lastCatchUp)
        {
            await Console.Error.WriteLineAsync(said);
            lastCatchUp = said;
        }
    }
}
