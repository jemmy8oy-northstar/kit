namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// The body of <c>GET /api/projects/{app}/schedule</c> (BEH-SCHED-1..3): the schedule in order, then
/// the backlog — every pending behaviour the schedule does not list, in corpus order.
/// </summary>
public interface IScheduleList
{
    string App { get; }

    /// <summary>Where the schedule lives, relative to the repository root — whether or not the file exists yet.</summary>
    string File { get; }

    /// <summary>Every id on the schedule, in order, built and vanished ones included — never dropped silently.</summary>
    IReadOnlyList<IScheduleEntry> Scheduled { get; }

    /// <summary>Pending behaviours not on the schedule.</summary>
    IReadOnlyList<IScheduleEntry> Backlog { get; }
}
