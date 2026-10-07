namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A later <c>provides</c> that disagrees with the value a symbol already holds.</summary>
public interface IChallenger
{
    string From { get; }

    IReadOnlyList<string> Value { get; }

    string At { get; }
}
