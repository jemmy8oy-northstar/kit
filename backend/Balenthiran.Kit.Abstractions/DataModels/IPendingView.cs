namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// <c>GET /api/projects/{app}/pending</c> (BEH-COMMIT-5): one project's share of the edits branch,
/// and which branches "pending" was measured between.
/// </summary>
public interface IPendingView : IPendingEdits
{
    string App { get; }

    /// <summary>The edits branch (<c>kit/hosted</c> when deployed), or null when Kit was not told one.</summary>
    string? Head { get; }

    /// <summary>The branch the edits are waiting to reach (<c>dev</c>).</summary>
    string Base { get; }
}
