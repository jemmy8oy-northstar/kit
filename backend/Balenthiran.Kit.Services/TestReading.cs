using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Services;

/// <summary>The titles read from a set of test files, or the refusal that the reader's two counts disagreed.</summary>
internal sealed class TestReading(IReadOnlyList<ITestTitle> titles, string? refusal) : ITestReading
{
    public IReadOnlyList<ITestTitle> Titles => titles;

    public string? Refusal => refusal;
}
