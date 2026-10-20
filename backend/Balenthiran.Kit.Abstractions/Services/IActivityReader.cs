using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>BEH-ACTIVITY-1: what changed recently, across every project this server lists.</summary>
public interface IActivityReader
{
    /// <summary>The body of <c>GET /api/activity</c>. Never throws: a project without readable history is a gap, not an error.</summary>
    IActivityFeed Recent();
}
