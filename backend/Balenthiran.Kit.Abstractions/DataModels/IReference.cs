namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A noun reference inside a step. `kind` is `literal` for a quoted string.</summary>
public interface IReference
{
    string Kind { get; }

    string Name { get; }
}
