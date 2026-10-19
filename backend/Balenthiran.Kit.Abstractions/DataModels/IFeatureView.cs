namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One feature as the project view serves it (kit#237).</summary>
public interface IFeatureView
{
    string Id { get; }

    string Title { get; }

    /// <summary>Parent feature ids, in the order the <c>in</c> lines were written.</summary>
    IReadOnlyList<string> In { get; }

    string At { get; }

    /// <summary>The feature's stage (FEAT-STAGES), absent when it is not on the stages board.</summary>
    string? Stage { get; }
}
