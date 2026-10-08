using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Everything the server decides about a request (<c>ui.js</c>'s <c>answer()</c> and
/// <c>received()</c>): the raw target through WHATWG parsing, the base-path strip, the
/// preflight, the CSRF refusal, the body, and the CORS headers. Pure functions, so they
/// are scored without a socket.
/// </summary>
public interface IKitHost
{
    /// <summary>
    /// Answer one request before any body is read. <paramref name="target"/> is the raw
    /// request target; <paramref name="origin"/> and <paramref name="cookie"/> the headers,
    /// or null when absent. A POST that may proceed comes back with only <c>Post</c> set.
    /// </summary>
    IHostAnswer Answer(string method, string target, string? origin, string? cookie);

    /// <summary>A POST's body has been read: <paramref name="raw"/> is its bytes, or null when it ran past the limit.</summary>
    IHostAnswer Received(string pathname, byte[]? raw, string? origin, string? cookie);

    /// <summary>A router response as it goes on the wire (<c>ui.js</c>'s <c>delivered()</c>).</summary>
    IHostAnswer Deliver(IKitResponse response, string? origin);
}
