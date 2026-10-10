using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>One test as a human would name it.</summary>
public sealed class TestTitle : ITestTitle
{
    public required string File { get; init; }

    public required int Line { get; init; }

    public required string Raw { get; init; }

    public required string Style { get; init; }
}
