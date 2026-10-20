namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A project the activity feed has no history for, and why (BEH-ACTIVITY-1).</summary>
public interface IActivityGap
{
    string App { get; }

    string Reason { get; }
}
