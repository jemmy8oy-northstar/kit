using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// BEH-ORCH-8: once Kit has started, triages the notes a project has left unsorted, so a note left while Kit was
/// down, or whose triage failed, does not wait for his next note. Runs once, off the start-up path: reading the
/// notes may mean asking GitHub, and Kit must not wait on that to serve.
/// </summary>
public sealed class PipelineResume(Pipeline pipeline) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            pipeline.Resume();
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"kit: resuming the pipeline threw {e.GetType().Name}");
        }
    }
}
