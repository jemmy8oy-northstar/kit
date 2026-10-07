namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>The requires report with each noun decorated for the page.</summary>
public interface IRequiresView
{
    IReadOnlyList<INounView> Nouns { get; }

    IReadOnlyList<INounView> Missing { get; }

    IReadOnlyList<INounView> Insufficient { get; }

    IReadOnlyList<string> Satisfied { get; }
}
