using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>Reads the tests out of a test file, and counts them a second, differently-shaped way.</summary>
public interface ITestTitleReader
{
    /// <summary>Whether a file name is one the gate reads: <c>*.spec|test.(ts|tsx|js|jsx)</c> or <c>*Test(s).cs</c>.</summary>
    bool IsTestFile(string name);

    /// <summary>Every test declared in <paramref name="src"/>, in source order (<c>kit.js</c>'s <c>testTitles</c>).</summary>
    IReadOnlyList<ITestTitle> Titles(string file, string src);

    /// <summary>
    /// The second count (<c>expectedTestCount</c>). When it disagrees with <see cref="Titles"/>
    /// the reader is losing or inventing tests, and the gate refuses rather than undercounting.
    /// </summary>
    int ExpectedCount(string file, string src);
}
