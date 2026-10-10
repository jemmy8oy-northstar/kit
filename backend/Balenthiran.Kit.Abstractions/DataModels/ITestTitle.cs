namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One test as a human would name it: where it is declared, and the title (or method) that names it.</summary>
public interface ITestTitle
{
    /// <summary>The file, relative to the repository the gate read, <c>/</c>-separated.</summary>
    string File { get; }

    /// <summary>1-based line of the declaration.</summary>
    int Line { get; }

    string Raw { get; }

    /// <summary><c>title</c>, <c>each</c>, <c>method</c> or <c>DisplayName</c>.</summary>
    string Style { get; }
}
