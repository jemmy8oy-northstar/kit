using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What the host puts on the wire for one request.</summary>
public sealed class HostAnswer : IHostAnswer
{
    public int Status { get; init; }

    public Dictionary<string, string> Headers { get; init; } = [];

    public byte[]? Raw { get; init; }

    public string? Body { get; init; }

    public string? Post { get; init; }

    IReadOnlyDictionary<string, string> IHostAnswer.Headers => Headers;
}
