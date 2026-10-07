using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>The parts of a WHATWG URL that Kit reads.</summary>
public sealed class ParsedUrl : IParsedUrl
{
    public required string Scheme { get; init; }

    public required string Host { get; init; }

    public required string Pathname { get; init; }

    public required string Origin { get; init; }
}
