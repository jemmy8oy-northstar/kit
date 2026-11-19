using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// BEH-ORCH-10: every <see cref="Interval"/>, asks the pipeline to merge the pull requests of work it built whose
/// checks have passed. Registered only with <c>KIT_PIPELINE_MERGE=on</c>. A tick that throws is said on stderr by
/// type only (an exception from GitHub's client may carry the token) and the next one still runs.
/// </summary>
public sealed class PipelineMerger(Pipeline pipeline) : BackgroundService
{
    /// <summary>How often the built pull requests are looked at.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await pipeline.MergeReadyAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await Console.Error.WriteLineAsync($"kit: the pipeline merger's tick failed — {e.GetType().Name}");
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
