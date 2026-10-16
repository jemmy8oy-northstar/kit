using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-BOARD-4: when each built behaviour of a project was completed.</summary>
public interface ICompletionReader
{
    /// <summary>The body of <c>GET /api/projects/{app}/history</c>; <paramref name="app"/> must be a known corpus.</summary>
    ICompletionList For(string app);
}
