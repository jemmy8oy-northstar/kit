namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One obligation a noun owes, the verbs that impose it, and whether the current binding meets it.</summary>
public interface INeed
{
    string Id { get; }

    string Surface { get; }

    IReadOnlyList<string> Verbs { get; }

    bool Met { get; }
}
