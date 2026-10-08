namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What the host puts on the wire for one request (<c>ui.js</c>'s <c>answer()</c>): a status,
/// every header it sets, and bytes or a serialised body. Or, for a POST under the base
/// path, only <see cref="Post"/> — the path whose body must be read before it can be answered.
/// </summary>
public interface IHostAnswer
{
    int Status { get; }

    /// <summary>Lower-case header names, exactly the ones <c>serve()</c> writes.</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Bundle bytes, sent as they are.</summary>
    byte[]? Raw { get; }

    /// <summary>The serialised JSON payload, or the empty string for a preflight.</summary>
    string? Body { get; }

    /// <summary>Set only for a POST under the base path: the path with the prefix removed.</summary>
    string? Post { get; }
}
