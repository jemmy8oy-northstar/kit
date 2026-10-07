using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Everything the server decides about a request before it reads a body (<c>ui.js</c>'s
/// <c>answer()</c>): the raw target through WHATWG parsing, the base-path strip, the
/// preflight, and the CORS headers. A pure function, so it is scored without a socket.
/// </summary>
public interface IKitHost
{
    /// <summary>
    /// Answer one request. <paramref name="target"/> is the raw request target;
    /// <paramref name="origin"/> the Origin header, or null when absent.
    /// </summary>
    IHostAnswer Answer(string method, string target, string? origin);

    /// <summary>A router response as it goes on the wire (<c>ui.js</c>'s <c>delivered()</c>).</summary>
    IHostAnswer Deliver(IKitResponse response, string? origin);
}
