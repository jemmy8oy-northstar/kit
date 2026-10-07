namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>How many behaviours are defined vs inferred, and the ids in each review state.</summary>
public interface IAdjudicationReport
{
    int Defined { get; }

    int Inferred { get; }

    IReadOnlyList<string> Unreviewed { get; }

    IReadOnlyList<string> Approved { get; }

    IReadOnlyList<string> Denied { get; }

    IReadOnlyList<string> Untraceable { get; }
}
