namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>What stage 3 returns for one behaviour: a Playwright test, and what it could not bind.</summary>
public interface IGeneratedTest
{
    /// <summary>The whole <c>test(...)</c> block, lines joined by <c>\n</c>, no trailing newline.</summary>
    string Code { get; }

    /// <summary>Every <c>kind:Name</c> with no binding, in the order generation first asked for it.</summary>
    IReadOnlyList<string> Missing { get; }

    IGenerateStats Stats { get; }
}
