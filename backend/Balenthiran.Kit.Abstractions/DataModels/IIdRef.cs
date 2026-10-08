namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A reference to another behaviour by id — what `serves` and `cites` both are.</summary>
public interface IIdRef
{
    string Id { get; }

    string At { get; }
}
