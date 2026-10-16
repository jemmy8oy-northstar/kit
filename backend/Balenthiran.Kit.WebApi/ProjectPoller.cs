using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;

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
/// </summary>
public sealed class ProjectPoller : BackgroundService
{
    /// <summary>How long a request waits for an expired read before it serves the last snapshot.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(3);

    private readonly GitHubCorpusDirectory directory;
    private readonly KitSettings settings;
    private readonly IKitRouter router;
    private readonly ExpiringRefresh refresh;
    private string? lastCatchUp;

    public ProjectPoller(GitHubCorpusDirectory directory, KitSettings settings, TimeProvider clock, IKitRouter router)
    {
        this.directory = directory;
        this.settings = settings;
        this.router = router;
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
