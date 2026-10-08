namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Coverage on a list row. `covered` is null — not 0 — when there was nothing to read.</summary>
public interface ICoverageSummary
{
    bool Available { get; }

    int? Covered { get; }

    int? Uncovered { get; }

    string? Reason { get; }
}
