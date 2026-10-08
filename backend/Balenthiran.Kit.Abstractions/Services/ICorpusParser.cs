using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>Stage 1 of the engine: corpus text in, behaviour tree out.</summary>
public interface ICorpusParser
{
    /// <summary>
    /// Parse a corpus. <paramref name="file"/> is used only to build the
    /// <c>file:line</c> locator every node carries, and defaults to the same
    /// <c>&lt;inline&gt;</c> the Node engine uses.
    /// </summary>
    /// <exception cref="CorpusParseException">The corpus is not a corpus.</exception>
    IReadOnlyList<IBehaviour> Parse(string text, string file = "<inline>");
}
