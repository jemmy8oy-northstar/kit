namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-ORCH-11: where a ticket is on its way from a note to a merged pull request.</summary>
public enum TicketState
{
    /// <summary>Triage opened it; nothing has added context yet.</summary>
    New,

    /// <summary>It has the context a planner needs.</summary>
    Ready,

    /// <summary>Its cards and test plan are written.</summary>
    Planned,

    /// <summary>Its code is written and its pull request opened.</summary>
    Built,

    /// <summary>Its pull request was merged.</summary>
    Merged,

    /// <summary>It stopped for a reason a person must read.</summary>
    Stopped,
}

/// <summary>
/// One ticket as the database keeps it (BEH-ORCH-11): the pipeline's work item, so that it survives a restart.
/// <paramref name="Bundle"/> is the item's context bundle rendered as markdown (null when it has none).
/// <paramref name="Scheduled"/> is set for a ticket Kit made itself from a scheduled card (BEH-ORCH-4).
/// </summary>
public sealed record PipelineTicket(
    string Project,
    string Slug,
    string Title,
    TicketState State,
    int Priority,
    IReadOnlyList<string> Cards,
    string? Bundle,
    string? PullRequest,
    string? StoppedBecause,
    bool Scheduled);

/// <summary>BEH-ORCH-11: the pipeline's tickets, kept so a restart does not lose them.</summary>
public interface ITicketStore
{
    /// <summary>Writes <paramref name="ticket"/>, replacing the ticket of the same project and slug.</summary>
    void Save(PipelineTicket ticket);

    /// <summary>Every ticket that is not merged or stopped, oldest first.</summary>
    IReadOnlyList<PipelineTicket> Open();

    /// <summary>Every ticket of <paramref name="project"/>, oldest first.</summary>
    IReadOnlyList<PipelineTicket> Read(string project);
}
