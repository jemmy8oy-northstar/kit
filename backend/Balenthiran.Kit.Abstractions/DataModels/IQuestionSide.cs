namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One side of a conflict, carrying its own citation and value so a reader needs no repo.</summary>
public interface IQuestionSide
{
    string Id { get; }

    string Title { get; }

    string? Ref { get; }

    IReadOnlyList<string> Value { get; }
}
