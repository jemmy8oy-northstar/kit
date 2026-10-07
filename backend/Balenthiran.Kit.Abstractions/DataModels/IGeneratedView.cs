namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One behaviour's generated test, keyed by its id.</summary>
public interface IGeneratedView
{
    string Id { get; }

    string Code { get; }

    IReadOnlyList<string> Missing { get; }

    IGenerateStats Stats { get; }
}
