namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>Whether anyone has ruled on this behaviour, and what they said.</summary>
public interface IReview
{
    string State { get; }

    string? Note { get; }
}
