namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One corpus file, parsed: its behaviours and the features they are tagged into.</summary>
public interface IParsedCorpus
{
    IReadOnlyList<IBehaviour> Behaviours { get; }

    IReadOnlyList<IFeature> Features { get; }

    /// <summary>The file's <c># kit:project &lt;name&gt;</c> directive (kit#237), or null when it has none.</summary>
    string? Project { get; }
}
