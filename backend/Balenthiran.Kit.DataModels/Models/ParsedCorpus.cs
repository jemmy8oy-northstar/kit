using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One corpus file, parsed: its behaviours and its features (kit#237).</summary>
public sealed class ParsedCorpus : IParsedCorpus
{
    public required List<Behaviour> Behaviours { get; init; }

    public required List<Feature> Features { get; init; }

    IReadOnlyList<IBehaviour> IParsedCorpus.Behaviours => Behaviours;

    IReadOnlyList<IFeature> IParsedCorpus.Features => Features;
}
