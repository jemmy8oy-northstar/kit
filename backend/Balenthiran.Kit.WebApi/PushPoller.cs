using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// BEH-NOTIFY-3: every <see cref="Interval"/>, hands the feed to <see cref="PushNotifier"/>, so a new
/// entry reaches his phone within a minute whether or not the page is open. Registered only when the
/// VAPID key pair is set; without it nothing polls. A tick that throws is said on stderr and the
/// next one still runs, because a poller that dies on one bad read pushes nothing again until a deploy.
/// </summary>
public sealed class PushPoller(PushNotifier notifier) : BackgroundService
{
    /// <summary>How often the feed is read for pushing.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await notifier.TickAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await Console.Error.WriteLineAsync($"kit: the push poller's tick failed — {e.GetType().Name}: {e.Message}");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
