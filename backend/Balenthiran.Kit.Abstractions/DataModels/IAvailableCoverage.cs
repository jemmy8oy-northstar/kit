namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Coverage that was measured (BEH-HEALTH-1): behaviour ids, not counts — the detail endpoint passes <c>project.js</c>'s raw result through.</summary>
public interface IAvailableCoverage : ICoverage
{
    /// <summary>Built behaviours a test names.</summary>
    IReadOnlyList<string> Covered { get; }

    /// <summary>Built behaviours no test names. Pending behaviours are in neither list.</summary>
    IReadOnlyList<string> Uncovered { get; }
}
