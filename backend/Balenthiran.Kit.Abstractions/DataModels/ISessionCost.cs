namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// BEH-COST-1: what one Claude Code session Kit ran cost, as the run's own result reported it, with the date, project
/// and model it ran on. One per session. It is not an estimate and not a subscription price.
/// </summary>
public interface ISessionCost
{
    /// <summary>The run the session was (the run's id, so one session has one record).</summary>
    string RunId { get; }

    /// <summary>When the session ended, as Kit recorded it.</summary>
    DateTimeOffset Date { get; }

    /// <summary>The repo the session worked on (owner/repo).</summary>
    string Project { get; }

    /// <summary>The model the session ran.</summary>
    string Model { get; }

    /// <summary>The dollar cost Claude Code reported for the session.</summary>
    decimal CostUsd { get; }
}
