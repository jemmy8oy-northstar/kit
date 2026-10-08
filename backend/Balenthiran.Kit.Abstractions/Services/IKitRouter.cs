using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Kit's HTTP surface as a pure function of the request (<c>ui.js</c>'s <c>route()</c>):
/// testable without a socket, and the one place a request becomes an answer. The host
/// strips its base path, reads the body, and delivers what this returns.
/// </summary>
public interface IKitRouter
{
    /// <summary>
    /// Answer one request. <paramref name="pathname"/> is the raw, still-encoded path, base
    /// path already removed; <paramref name="body"/> is the parsed JSON body of a POST.
    /// </summary>
    IKitResponse Route(string method, string pathname, string? cookie = null, string? origin = null, JsonElement? body = null);
}
