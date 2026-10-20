namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One commit's effect on one project, as the activity feed shows it (BEH-ACTIVITY-1).</summary>
public interface IActivityEntry
{
    /// <summary>The abbreviated commit hash.</summary>
    string Commit { get; }

    /// <summary>ISO 8601 UTC: when the commit landed on this branch.</summary>
    string At { get; }

    string App { get; }

    /// <summary>The commit's own subject line.</summary>
    string Summary { get; }
}
