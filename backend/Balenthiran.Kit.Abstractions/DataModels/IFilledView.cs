namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A hole another behaviour filled, as the project view serves it.</summary>
public interface IFilledView
{
    string Key { get; }

    IReadOnlyList<string> Value { get; }
}
