namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Three populations kept apart: no binding, a binding that meets no verb, and generatable.</summary>
public interface IRequiresReport
{
    IReadOnlyList<INounRequirement> Nouns { get; }

    IReadOnlyList<INounRequirement> Missing { get; }

    IReadOnlyList<INounRequirement> Insufficient { get; }

    IReadOnlyList<string> Satisfied { get; }
}
