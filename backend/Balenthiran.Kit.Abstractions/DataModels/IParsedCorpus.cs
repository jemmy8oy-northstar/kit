namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One corpus file, parsed: its behaviours and the features they are tagged into.</summary>
public interface IParsedCorpus
{
    IReadOnlyList<IBehaviour> Behaviours { get; }

    IReadOnlyList<IFeature> Features { get; }
}
