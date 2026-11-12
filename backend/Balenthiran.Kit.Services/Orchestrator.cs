namespace Balenthiran.Kit.Services;

/// <summary>The stages a work item passes through (kit-ui.beh <c>BEH-ORCH-3</c>). Review is entered only from a flag.</summary>
public enum PipelineStage
{
    /// <summary>Reads and sorts the task into a context bundle (BEH-ORCH-1).</summary>
    Triage,

    /// <summary>A stronger model checks a flagged stage's bundle (BEH-ORCH-2).</summary>
    Review,

    /// <summary>
    /// BEH-ORCH-11: adds to a triaged ticket's bundle what a planner needs. Only entered with
    /// <c>KIT_PIPELINE_JOB_CONTEXT=on</c>, by the context job; otherwise a triaged item goes straight to Spec.
    /// </summary>
    Context,

    /// <summary>Formalises the cards and plans the tests.</summary>
    Spec,

    /// <summary>Tests first, then code, until they pass.</summary>
    Build,

    /// <summary>
    /// BEH-ORCH-11, slice 7: a job. Owning it (<c>KIT_PIPELINE_JOB_MERGE=on</c>) moves the merge of a built item's pull
    /// request from <c>MergeReadyAsync</c> to <c>MergeTickAsync</c>. Slice 7b: also the stage of a work item for exactly as
    /// long as its conflict-resolution run is in flight (it is a built ticket meanwhile), and Build, Done again after.
    /// </summary>
    Merge,
}

/// <summary>Where a work item is within its current stage.</summary>
public enum WorkItemStatus
{
    /// <summary>Waiting to be started.</summary>
    Queued,

    /// <summary>One agent run is working on it.</summary>
    Running,

    /// <summary>It stopped for a reason a person must read: a failed run, an escalation, or output the next stage cannot use.</summary>
    Stopped,

    /// <summary>Every stage has finished.</summary>
    Done,
}

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>: the orchestrator is plain code, not a model. A pure state machine over
/// <see cref="WorkItem"/>s: it decides which item may start and what a finished run leads to, and stops at a flag
/// until the review has answered. Pure: nothing here starts a run, reads a clock or waits on a timer; a person's
/// action is what queues the next stage. Not used by anything yet.
/// </summary>
public static class Orchestrator
{
    /// <summary>
    /// The item after a run of its stage ended with <paramref name="result"/>. THROWS unless the item is Running
    /// and the result is for the stage it is running.
    /// </summary>
    public static WorkItem Advance(WorkItem item, StageResult result)
    {
        if (item.Status != WorkItemStatus.Running) throw new InvalidOperationException($"only a running item can finish a stage, not one that is {item.Status}");
        if (result.Stage != item.Stage) throw new InvalidOperationException($"the item is running {item.Stage} but the result is for {result.Stage}");

        if (!result.Succeeded) return Stop(item, string.IsNullOrWhiteSpace(result.Error) ? "the run failed" : result.Error);

        var bundle = result.Bundle;
        if (item.Stage == PipelineStage.Review)
        {
            if (bundle?.Review is not { } review) return Stop(item, "the review left no Review line");
            if (review.Outcome == ReviewOutcome.Escalated) return Stop(item, review.Note);
            var reviewed = item.ReviewedStage ?? throw new InvalidOperationException("a review run must say which stage it is reviewing");
            return Next(item, reviewed);
        }

        if (bundle is null)
        {
            return item.Stage == PipelineStage.Build ? Next(item, item.Stage) : Stop(item, "the stage left no bundle");
        }

        return bundle.Flag is not null && bundle.Review is null
            ? item with { Stage = PipelineStage.Review, Status = WorkItemStatus.Queued, ReviewedStage = item.Stage }
            : Next(item, item.Stage);
    }

    /// <summary>
    /// The queued items that may start now: at most one per project, and none for a project that already has a
    /// running item. The input order is the queue order, so a project's first queued item is the one picked and
    /// the result keeps the input order across projects.
    /// </summary>
    public static IReadOnlyList<WorkItem> Startable(IEnumerable<WorkItem> items)
    {
        var all = items.ToList();
        var taken = all.Where(i => i.Status == WorkItemStatus.Running).Select(i => i.Project).ToHashSet(StringComparer.Ordinal);
        var startable = new List<WorkItem>();
        foreach (var i in all.Where(i => i.Status == WorkItemStatus.Queued))
        {
            if (taken.Add(i.Project))
            {
                startable.Add(i);
            }
        }

        return startable;
    }

    /// <summary>The item as a run begins. THROWS unless it is Queued.</summary>
    public static WorkItem Start(WorkItem item) =>
        item.Status == WorkItemStatus.Queued
            ? item with { Status = WorkItemStatus.Running }
            : throw new InvalidOperationException($"only a queued item can start, not one that is {item.Status}");

    /// <summary>The model <paramref name="stage"/> runs on; a review of a low-level <paramref name="flag"/> runs on the cheaper review model (BEH-ORCH-2).</summary>
    public static string ModelFor(PipelineStage stage, StageModelSettings models, string? flag = null) => stage switch
    {
        PipelineStage.Triage => models.Triage,
        PipelineStage.Review => FlagLevels.Of(flag) == FlagLevel.Low ? models.ReviewLow ?? StageModels.DefaultReviewLow : models.Review,
        PipelineStage.Context => models.Context ?? StageModels.DefaultContext,
        PipelineStage.Spec => models.Spec,
        PipelineStage.Build => models.Build,
        PipelineStage.Merge => models.Resolve ?? models.Build,
        _ => throw new InvalidOperationException($"no model is set for stage {stage}"),
    };

    /// <summary>The turn limit of <paramref name="stage"/>'s run (BEH-ORCH-5).</summary>
    public static int TurnsFor(PipelineStage stage, StageTurnSettings turns) => stage switch
    {
        PipelineStage.Triage => turns.Triage,
        PipelineStage.Review => turns.Review,
        PipelineStage.Context => turns.Context,
        PipelineStage.Spec => turns.Spec,
        PipelineStage.Build => turns.Build,
        PipelineStage.Merge => turns.Resolve ?? turns.Build,
        _ => throw new InvalidOperationException($"no turn limit is set for stage {stage}"),
    };

    private static WorkItem Stop(WorkItem item, string because) =>
        item with { Status = WorkItemStatus.Stopped, StoppedBecause = because };

    /// <summary>The item queued for the stage after <paramref name="finished"/>; after Build it is Done on Build.</summary>
    private static WorkItem Next(WorkItem item, PipelineStage finished) => finished switch
    {
        PipelineStage.Triage => item with { Stage = PipelineStage.Spec, Status = WorkItemStatus.Queued, ReviewedStage = null },
        PipelineStage.Context => item with { Stage = PipelineStage.Spec, Status = WorkItemStatus.Queued, ReviewedStage = null },
        PipelineStage.Spec => item with { Stage = PipelineStage.Build, Status = WorkItemStatus.Queued, ReviewedStage = null },
        PipelineStage.Build => item with { Stage = PipelineStage.Build, Status = WorkItemStatus.Done, ReviewedStage = null },
        _ => throw new InvalidOperationException($"nothing follows {finished}"),
    };
}
