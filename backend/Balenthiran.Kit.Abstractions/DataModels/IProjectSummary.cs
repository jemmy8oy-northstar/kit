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
}
