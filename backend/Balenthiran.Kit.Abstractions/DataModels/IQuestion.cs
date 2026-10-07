namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// One row of the question sheet. Two shapes share it — a conflict between two
/// behaviours, and a question about one inference — and their other fields differ.
/// </summary>
public interface IQuestion
{
    /// <summary><c>conflict</c>, <c>unserved</c> or <c>review</c>.</summary>
    string Kind { get; }

    /// <summary><c>decision</c> or <c>review</c>; decisions sort first.</summary>
    string Tier { get; }

    string Key { get; }

    string Title { get; }
}
