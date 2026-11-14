using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// BEH-ORCH-11, slices 3 to 7 (Merge: <see cref="Pipeline.MergeTickAsync"/>): every <see cref="Interval"/>, asks the pipeline to take, per project, the waiting ticket of
/// highest priority to its run: Context for the context job, Spec for the plan job. One is registered for each job switch
/// that is on (<c>KIT_PIPELINE_JOB_CONTEXT</c>, <c>KIT_PIPELINE_JOB_PLAN</c>, <c>KIT_PIPELINE_JOB_BUILD</c>). A tick that throws is said on stderr by type
/// only and the next one still runs. A tick with nothing waiting starts no run.
/// </summary>
public sealed class PipelineJob(Pipeline pipeline, PipelineStage stage, TimeSpan? interval = null) : BackgroundService
{
    /// <summary>How often the waiting tickets are looked at, unless <c>KIT_PIPELINE_JOB_MINUTES</c> says otherwise.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval ?? DefaultInterval);
        do
        {
            try
            {
                if (stage == PipelineStage.Merge)
                {
                    await pipeline.MergeTickAsync(stoppingToken);
                }
                else
                {
                    pipeline.JobTick(stage);
                }
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"kit: the pipeline's {stage.ToString().ToLowerInvariant()} job tick failed — {e.GetType().Name}");
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
