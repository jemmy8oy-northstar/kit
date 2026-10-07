namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Which inferences serve a documented behaviour, which serve nothing, and every broken `serves` link.</summary>
public interface ISurfaceReport
{
    IReadOnlyList<string> Errors { get; }

    IReadOnlyList<string> Served { get; }

    IReadOnlyList<string> Unserved { get; }
}
