namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The body of <c>GET /api/runs/visibility</c> (BEH-VIS-1, BEH-VIS-2): the agents running now, and the work items whose
/// pipeline run has finished, each with what each stage cost and the reviews it requested. Operator only.
/// </summary>
public interface IRunVisibility
{
    /// <summary>Every agent run that is running now, newest first (BEH-VIS-1).</summary>
    IReadOnlyList<IRunningAgent> Running { get; }

    /// <summary>Every work item whose latest pipeline run has finished, newest first (BEH-VIS-2).</summary>
    IReadOnlyList<IFinishedRun> Finished { get; }

    /// <summary>Every run, newest start first: the runs page's cards (BEH-UI-7, BEH-UI-8).</summary>
    IReadOnlyList<IRecentRun> Recent { get; }
}

/// <summary>One run on the runs page's cards (BEH-UI-7, BEH-UI-8).</summary>
public interface IRecentRun
{
    string Id { get; }

    string Project { get; }

    /// <summary>The work item the run is for; null for triage and for a run started by hand.</summary>
    string? WorkItem { get; }

    /// <summary>The pipeline stage; null for a run started by hand.</summary>
    string? Stage { get; }

    string Model { get; }

    /// <summary>The state the run record holds: queued, running, finished or failed.</summary>
    string Status { get; }

    DateTimeOffset Started { get; }

    DateTimeOffset? Ended { get; }

    /// <summary>Input, output and cache tokens together.</summary>
    long Tokens { get; }

    /// <summary>Why a failed run failed, as its runner reported it; null otherwise.</summary>
    string? Failure { get; }

    /// <summary>The run's recorded session cost (BEH-COST-1); null when none is recorded: unknown, never zero.</summary>
    decimal? CostUsd { get; }
}

/// <summary>One agent that is running (BEH-VIS-1): where it is, and any review its work item's bundle flagged.</summary>
public interface IRunningAgent
{
    string Id { get; }

    string Project { get; }

    /// <summary>The work item the run is for; null for a run that is not for one item, such as triage.</summary>
    string? WorkItem { get; }

    /// <summary>The stage it is at now (triage, spec, build, review, ...); null for a run started by hand.</summary>
    string? Stage { get; }

    /// <summary>The model the run uses, which is the agent's name here.</summary>
    string Model { get; }

    DateTimeOffset Started { get; }

    /// <summary>The bundle's Flag line, without "Flag: "; null when no review was flagged, so no review line is shown.</summary>
    string? Flag { get; }

    /// <summary>The agent that flagged it: the model of the stage run that came before this one on the item. Null when there is none.</summary>
    string? FlaggedBy { get; }
}

/// <summary>One work item's pipeline run once it has finished (BEH-VIS-2).</summary>
public interface IFinishedRun
{
    string Project { get; }

    string WorkItem { get; }

    /// <summary>Each stage run of the item, oldest first, with what it cost.</summary>
    IReadOnlyList<IFinishedStage> Stages { get; }

    /// <summary>The reviews the item requested, oldest first.</summary>
    IReadOnlyList<IRequestedReview> Reviews { get; }
}

/// <summary>One stage run of a finished item. <see cref="CostUsd"/> is null when no cost is recorded: unknown, never zero.</summary>
public interface IFinishedStage
{
    string RunId { get; }

    string? Stage { get; }

    string Model { get; }

    /// <summary>What the stage cost, USD, from the session cost record (BEH-COST-1); null when none is recorded.</summary>
    decimal? CostUsd { get; }
}

/// <summary>A review a finished item requested: the stage that asked for it and the agent that did.</summary>
public interface IRequestedReview
{
    string RunId { get; }

    /// <summary>The stage that requested the review.</summary>
    string? Stage { get; }

    /// <summary>The agent (model) that requested it.</summary>
    string? RequestedBy { get; }
}
