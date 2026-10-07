namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A hole in a step that another behaviour's <c>provides</c> filled.</summary>
public interface IFilled
{
    string Key { get; }

    IReadOnlyList<string> Value { get; }

    IReadOnlyList<string> From { get; }

    string At { get; }
}
