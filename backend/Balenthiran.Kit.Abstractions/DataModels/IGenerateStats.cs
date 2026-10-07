namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>How each step of one behaviour came out of <c>generate</c>.</summary>
public interface IGenerateStats
{
    /// <summary>Lines written — not steps: a <c>fills</c> over three fields writes three.</summary>
    int Generated { get; }

    /// <summary>Wire contracts: commented, counted, never generated.</summary>
    int Contract { get; }

    /// <summary>Steps Kit refused rather than guessed.</summary>
    int Ungenerated { get; }
}
