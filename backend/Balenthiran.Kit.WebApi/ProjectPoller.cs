using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// Re-reads the <c>KIT_PROJECTS</c> sources when a request finds the last read older than
/// <see cref="KitSettings.ProjectsPoll"/> (kit#300, BEH-PULL-5) — not on a timer, so an idle Kit
/// asks GitHub for nothing. The first read runs at start-up, in the background: until it lands the
/// clone answers, so a slow or absent GitHub delays fresh projects rather than the server. A failed
/// read is said on stderr and the last good snapshot keeps serving.
///
/// Before each read it brings the edits branch up to date with dev (kit#300, BEH-COMMIT-6), so a
/// spec merged into dev is on the branch the read is about to fetch. What that did is said on
/// stderr only when it differs from last time: "already up to date" on every read is noise,
/// and a conflict said once, then again only when it clears or changes, is still said.
///
/// Last, it re-reads the tests repos the corpora name (BEH-HEALTH-1) into <see cref="CoverageStore"/>,
/// so coverage rides the same interval and the same request-driven clock as the corpora — in the
/// background, never waited on by a request.
/// </summary>
public sealed class ProjectPoller : BackgroundService
{
    /// <summary>How long a request waits for an expired read before it serves the last snapshot.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(3);

    private readonly GitHubCorpusDirectory directory;
    private readonly KitSettings settings;
    private readonly IKitRouter router;
    private readonly CoverageStore? coverage;
    private readonly ExpiringRefresh? coverageRefresh;
    private readonly ExpiringRefresh refresh;
    private string? lastCatchUp;

    public ProjectPoller(GitHubCorpusDirectory directory, KitSettings settings, TimeProvider clock, IKitRouter router, CoverageStore? coverage = null)
    {
        this.directory = directory;
        this.settings = settings;
        this.router = router;
        this.coverage = coverage;
        coverageRefresh = coverage is null ? null : new ExpiringRefresh(CoverageAsync, TimeSpan.Zero, clock);
        refresh = new ExpiringRefresh(ReadAsync, settings.ProjectsPoll, clock);
    }

    /// <summary>Called by every /api request before it is answered; see <see cref="Wait"/>.</summary>
    public Task<bool> EnsureFreshAsync(CancellationToken cancellationToken) => refresh.EnsureFreshAsync(Wait, cancellationToken);

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => refresh.EnsureFresh();

    private async Task ReadAsync()
    {
        await CatchUpAsync();
        try
        {
            await directory.RefreshAsync();
        }
        catch (Exception e)
        {
            // Nothing awaits this read to the end, so a throw here would be lost, not said.
            var serving = directory.FromGitHub ? "the last snapshot" : "the clone";
            var why = e is GitHubReadException ? e.Message : $"{e.GetType().Name}: {e.Message}";
            await Console.Error.WriteLineAsync($"kit: could not read KIT_PROJECTS — {why}; serving {serving}");
        }

        // BEH-HEALTH-1: the tests repos the corpora name, started AFTER the corpora (their
        // `# kit:repo` lines are what it follows) but not awaited: the first read fetches every test
        // file, and a request must not wait on that. Zero max-age because this read's own clock
        // already decided it is due; in flight, it is joined rather than doubled.
        _ = coverageRefresh?.EnsureFresh();
    }

    private async Task CoverageAsync()
    {
        try
        {
            await coverage!.RefreshAsync();
        }
        catch (Exception e)
        {
            // The store says its own failures per repo (the viewer shows them); this is a bug in the read.
            await Console.Error.WriteLineAsync($"kit: could not read the tests repos — {e.GetType().Name}: {e.Message}");
        }
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
            // A catch-up that throws must not stop the read below.
            said = $"kit: catching up with {settings.GitBase} failed — {e.Message}";
        }

        if (said != lastCatchUp)
        {
            await Console.Error.WriteLineAsync(said);
            lastCatchUp = said;
        }
    }
}
