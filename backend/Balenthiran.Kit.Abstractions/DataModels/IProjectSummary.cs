namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One row of the project list: counts, not the projection.</summary>
public interface IProjectSummary
{
    string App { get; }

    string Corpus { get; }

    bool NotReal { get; }

    string? DuplicateOf { get; }

    int Behaviours { get; }

    int Conflicts { get; }

    ICoverageSummary Coverage { get; }

    int Unreviewed { get; }

    /// <summary>
    /// The project this corpus is shown under (kit#237): its <c># kit:project</c> directive, or
    /// its own name when it has none. Files sharing one are one project.
    /// </summary>
    string Project { get; }
}
