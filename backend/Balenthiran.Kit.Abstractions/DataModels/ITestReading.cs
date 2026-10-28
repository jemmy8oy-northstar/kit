namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// Every test title read out of a set of test files — or, when the reader's two counts disagree,
/// the refusal that says so. A reader that is losing or inventing tests must never produce a number.
/// </summary>
public interface ITestReading
{
    IReadOnlyList<ITestTitle> Titles { get; }

    /// <summary>Why the reading cannot be trusted (<c>kit check</c>'s <c>cannot look</c> sentence), or null.</summary>
    string? Refusal { get; }
}
