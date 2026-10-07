namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Everything one noun owes, merged across every step that uses it.</summary>
public interface INounRequirement
{
    string Noun { get; }

    string Kind { get; }

    string Name { get; }

    IReadOnlyList<string> UsedBy { get; }

    bool Bound { get; }

    bool Satisfied { get; }

    IReadOnlyList<INeed> Needs { get; }
}
