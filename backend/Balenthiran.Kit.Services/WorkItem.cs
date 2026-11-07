namespace Balenthiran.Kit.Services;

/// <summary>
/// One task moving through the pipeline. <paramref name="ReviewedStage"/> is the stage whose output a Review run is
/// checking, so the machine knows where to resume; it is null outside a review. <paramref name="PullRequest"/> is
/// the page of the pull request Kit opened once the item was built; <paramref name="Merged"/> is set once Kit merged it
/// (BEH-ORCH-10).
/// </summary>
public sealed record WorkItem(
    string Project,
    string Slug,
    PipelineStage Stage,
    WorkItemStatus Status,
    PipelineStage? ReviewedStage = null,
    string? StoppedBecause = null,
    string? PullRequest = null,
    bool Merged = false);
