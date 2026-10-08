namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One behaviour a question cites as evidence. A cited id that is not in the corpus keeps its id as its title.</summary>
public interface ICitation
{
    string Id { get; }

    string Title { get; }

    string? Ref { get; }

    IReadOnlyList<string> Contracts { get; }
}
