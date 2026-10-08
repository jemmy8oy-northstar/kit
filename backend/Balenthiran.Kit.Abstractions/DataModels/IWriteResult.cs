using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What an edit produced (<c>writer.js</c>'s result): the whole new text when
/// <see cref="Ok"/>, or a refusal naming what was wrong with the request. Never both,
/// and never a write — the caller writes only on <see cref="Ok"/>.
/// </summary>
public interface IWriteResult
{
    bool Ok { get; }

    string? Text { get; }

    string? Error { get; }

    string? Reason { get; }

    /// <summary>The behaviour ids that do exist, on <c>no-such-behaviour</c>.</summary>
    IReadOnlyList<string>? Known { get; }

    /// <summary>What a noun is already bound to, on <c>already-bound</c>.</summary>
    JsonNode? Current { get; }

    /// <summary>The bound noun, trimmed, on a successful bind.</summary>
    string? Noun { get; }

    /// <summary>Other corpora that use the bound noun's name, sorted, on a successful bind.</summary>
    IReadOnlyList<string>? SharedWith { get; }
}
