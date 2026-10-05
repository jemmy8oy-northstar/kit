namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Whether a human wrote this behaviour or something inferred it, and from what.</summary>
public interface ISource
{
    string Origin { get; }

    string? Ref { get; }
}
