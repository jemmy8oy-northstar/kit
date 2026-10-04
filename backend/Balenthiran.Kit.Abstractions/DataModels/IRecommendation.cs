namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A recommendation and why. One per behaviour, or none.</summary>
public interface IRecommendation
{
    string Label { get; }

    string Why { get; }

    string At { get; }
}
