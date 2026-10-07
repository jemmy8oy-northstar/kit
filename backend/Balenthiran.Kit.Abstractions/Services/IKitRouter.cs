using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Kit's HTTP surface as a pure function of method and path (<c>ui.js</c>'s <c>route()</c>):
/// testable without a socket, and the one place a request becomes an answer. The host
/// only strips its base path and delivers what this returns.
/// </summary>
public interface IKitRouter
{
    /// <summary>Answer one request. <paramref name="pathname"/> is the raw, still-encoded path, base path already removed.</summary>
    IKitResponse Route(string method, string pathname);
}
