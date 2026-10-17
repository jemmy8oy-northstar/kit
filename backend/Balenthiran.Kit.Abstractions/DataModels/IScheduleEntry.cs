namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One behaviour on the schedule or in the backlog.</summary>
public interface IScheduleEntry
{
    string Id { get; }

    /// <summary>The behaviour's title, or null when the corpus no longer has the id.</summary>
    string? Title { get; }

    /// <summary>
    /// <c>pending</c>, still to build; <c>done</c>, scheduled but no longer pending; or <c>missing</c>,
    /// scheduled but no longer in the corpus at all. Backlog entries are always <c>pending</c>.
    /// </summary>
    string State { get; }

    /// <summary>True for exactly one entry while anything scheduled is pending: the first such, the one an agent picks up.</summary>
    bool Next { get; }
}
