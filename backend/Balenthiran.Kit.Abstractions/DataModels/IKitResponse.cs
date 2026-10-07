namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What the router answers — the shape <c>ui.js</c>'s <c>route()</c> returns: a JSON
/// payload in <see cref="Body"/>, or bytes from the bundle in <see cref="Raw"/>, never both.
/// </summary>
public interface IKitResponse
{
    int Status { get; }

    string ContentType { get; }

    /// <summary>Set only for bundle files; null means the client may cache by its own rules.</summary>
    string? CacheControl { get; }

    object? Body { get; }

    /// <summary>The bytes to send, set only by the bundle — its presence is what distinguishes bytes from a payload.</summary>
    byte[]? Raw { get; }
}
