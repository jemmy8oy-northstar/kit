using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// Re-reads the <c>KIT_PROJECTS</c> sources every <see cref="KitSettings.ProjectsPoll"/> (kit#88).
/// The first read runs at start-up, in the background: until it lands the clone answers, so a slow
/// or absent GitHub delays fresh projects rather than the server. A failed read is said on stderr
/// and the last good snapshot keeps serving.
/// </summary>
public sealed class ProjectPoller(GitHubCorpusDirectory directory, KitSettings settings, TimeProvider clock) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(settings.ProjectsPoll, clock);
        do
        {
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
}
