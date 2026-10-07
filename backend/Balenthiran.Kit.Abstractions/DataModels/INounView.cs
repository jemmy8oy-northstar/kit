using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What a noun owes, plus its binding and the other corpora that name it.</summary>
public interface INounView
{
    string Noun { get; }

    string Kind { get; }

    string Name { get; }

    IReadOnlyList<string> UsedBy { get; }

    bool Bound { get; }

    bool Satisfied { get; }

    IReadOnlyList<INeed> Needs { get; }

    JsonNode? Binding { get; }

    IReadOnlyList<string> SharedWith { get; }
}
