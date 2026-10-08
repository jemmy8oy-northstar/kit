using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What an edit produced: the new text, or a refusal.</summary>
public sealed class WriteResult : IWriteResult
{
    public bool Ok { get; init; }

    public string? Text { get; init; }

    public string? Error { get; init; }

    public string? Reason { get; init; }

    public List<string>? Known { get; init; }

    public JsonNode? Current { get; init; }

    public string? Noun { get; init; }

    public List<string>? SharedWith { get; init; }

    IReadOnlyList<string>? IWriteResult.Known => Known;

    IReadOnlyList<string>? IWriteResult.SharedWith => SharedWith;

    public static WriteResult Refuse(string error, string reason, List<string>? known = null) =>
        new() { Ok = false, Error = error, Reason = reason, Known = known };
}
