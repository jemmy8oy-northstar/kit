namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The most recent PR that improved a project's test health (BEH-HEALTH-2), as its <c>tests.json</c> names it.</summary>
public interface ILastImprovedBy
{
    /// <summary>The PR's short name, e.g. <c>kit#538</c>.</summary>
    string Pr { get; }

    /// <summary>Where the PR lives.</summary>
    string Url { get; }

    /// <summary>The day it landed, <c>yyyy-MM-dd</c>.</summary>
    string On { get; }
}
