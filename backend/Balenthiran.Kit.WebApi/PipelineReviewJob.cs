using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-13</c>: every <see cref="Interval"/>, asks the pipeline to start the scheduled review of each project
/// whose cadence says one is due. Registered only with <c>KIT_PIPELINE_REVIEW=on</c>. The cadences are days, so a look every
/// fifteen minutes is as exact as the Settings page needs; a look that throws is said on stderr by type only, and the next still runs.
/// </summary>
public sealed class PipelineReviewJob(Pipeline pipeline, TimeSpan? interval = null) : BackgroundService
{
    /// <summary>How often the cadences are looked at.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval ?? DefaultInterval);
        do
        {
            try
            {
                pipeline.ReviewTick();
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"kit: the pipeline's review tick failed — {e.GetType().Name}");
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
