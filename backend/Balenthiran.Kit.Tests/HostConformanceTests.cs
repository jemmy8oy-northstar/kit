using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The host layer's score: every request in <c>conformance/routes/host.json</c>, which
/// <c>conformance.js</c> records from <c>ui.js</c>'s <c>answer()</c> — the function
/// <c>serve()</c> calls for every request — answered by <see cref="KitHost"/>, and then
/// delivered over a real socket by Kestrel, raw target and all.
/// </summary>
public class HostConformanceTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    /// <summary>Headers the transport adds on its own, in Node and in Kestrel alike.</summary>
    private static readonly HashSet<string> TransportOwn = ["date", "connection", "keep-alive", "content-length", "transfer-encoding"];

    /// <summary>
    /// Targets Kestrel refuses ITSELF, before the app runs, with an empty body — the
    /// counterpart of the ones Node's parser refuses, which <c>conformance.js</c> leaves
    /// out. Each refuses MORE than Node, never less: an absolute-form target whose host
    /// is not the Host header (RFC 9112 §3.2.2), <c>GET *</c> (asterisk-form is OPTIONS
    /// only), a NUL in the target, and an unclosed IPv6 bracket. Behind the ingress none
    /// of these forms reaches the pod anyway.
    /// </summary>
    private static readonly Dictionary<(string Method, string Target), int> KestrelRefuses = new()
    {
        [("GET", "http://other:99/kit/api/health")] = 400,
        [("GET", "*")] = 405,
        [("GET", "//exa%00mple/kit")] = 400,
        [("GET", "http://[::1/kit")] = 400,
    };

    private static string FixtureDist => Path.Combine(RepoLayout.Conformance, "routes", "dist");

    public static TheoryData<int> Rows()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Golden()["requests"]!.AsArray().Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_host_answers_as_ui_js_does(int row)
    {
        var q = Golden()["requests"]![row]!;
        var (basePath, publicOrigin) = Config(q["config"]!.GetValue<string>());
        var expected = q["response"]!;

        var a = Host(basePath, publicOrigin).Answer(q["method"]!.GetValue<string>(), q["url"]!.GetValue<string>(), q["origin"]?.GetValue<string>());

        if (expected["post"] is { } post)
        {
            Assert.Equal(post.GetValue<string>(), a.Post);
            return;
        }

        Assert.Null(a.Post);
        Assert.Equal(expected["status"]!.GetValue<int>(), a.Status);
        Assert.Equal(Headers(expected), a.Headers.OrderBy(h => h.Key, StringComparer.Ordinal).ToList());
        if (expected["rawText"] is { } raw)
        {
            Assert.Equal(raw.GetValue<string>(), Encoding.UTF8.GetString(a.Raw!));
        }
        else if (expected["body"] is { } body)
        {
            Assert.Equal(Canonical(body), Canonical(JsonNode.Parse(a.Body!)));
        }
        else
        {
            Assert.Equal(string.Empty, a.Body);
        }
    }

    /// <summary>
    /// The far side of the seam: Kestrel delivers what <see cref="KitHost"/> answers, for
    /// every row, sent as raw bytes so the target arrives exactly as the golden names it
    /// (an HttpClient would normalise <c>..</c> and backslashes away before sending).
    /// Every header the golden records, and none it does not beyond the transport's own.
    /// </summary>
    [Fact]
    public async Task Kestrel_delivers_every_answer_over_a_real_socket()
    {
        var mismatches = new List<string>();
        var sent = 0;
        foreach (var config in new[] { "root", "deployed" })
        {
            var (basePath, publicOrigin) = Config(config);
            await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], new KitSettings(RepoLayout.Behaviours, RepoLayout.Root, null, FixtureDist, basePath, publicOrigin));
            await app.StartAsync();
            var port = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

            foreach (var q in Golden()["requests"]!.AsArray().Where(r => r!["config"]!.GetValue<string>() == config && r["response"]!["post"] is null))
            {
                var label = $"{config} {q!["method"]} {q["url"]} origin={q["origin"]?.GetValue<string>() ?? "(none)"}";
                var expected = q["response"]!;
                var (status, headers, text) = await Send(port, q["method"]!.GetValue<string>(), q["url"]!.GetValue<string>(), q["origin"]?.GetValue<string>());
                sent++;

                if (KestrelRefuses.TryGetValue((q["method"]!.GetValue<string>(), q["url"]!.GetValue<string>()), out var refusal))
                {
                    // Pinned, not skipped: if Kestrel ever lets one through, it is scored again.
                    if (status != refusal || headers.ContainsKey("content-type"))
                    {
                        mismatches.Add($"{label}: Kestrel no longer refuses this itself (status {status}) — remove it from KestrelRefuses");
                    }

                    continue;
                }

                var want = Headers(expected).ToDictionary(h => h.Key, h => h.Value);
                var extra = headers.Keys.Where(h => !TransportOwn.Contains(h) && !want.ContainsKey(h)).ToList();
                var wrong = want.Where(h => !headers.TryGetValue(h.Key, out var v) || v != h.Value).Select(h => h.Key).ToList();
                var bodyOk = expected["rawText"] is { } raw ? text == raw.GetValue<string>()
                    : expected["body"] is { } body ? text.Length > 0 && Canonical(body) == Canonical(JsonNode.Parse(text))
                    : text.Length == 0;

                if (status != expected["status"]!.GetValue<int>() || extra.Count > 0 || wrong.Count > 0 || !bodyOk)
                {
                    mismatches.Add($"{label}: status {status} (want {expected["status"]}), extra [{string.Join(", ", extra)}], wrong [{string.Join(", ", wrong)}], body {(bodyOk ? "ok" : text[..Math.Min(80, text.Length)])}");
                }
            }

            await app.StopAsync();
        }

        Assert.True(sent >= 60, $"only {sent} rows went over the wire — the gate is inert");
        Assert.True(mismatches.Count == 0, string.Join('\n', mismatches));
    }

    /// <summary>Rule 8's normaliser: generous in, one spelling out.</summary>
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("kit", "/kit")]
    [InlineData("/kit/", "/kit")]
    [InlineData("//kit//", "/kit")]
    [InlineData(" kit ", "/kit")]
    [InlineData("﻿kit", "/kit")]
    [InlineData("/a/b/", "/a/b")]
    public void NormaliseBasePath_matches_ui_js(string? value, string expected) =>
        Assert.Equal(expected, KitHost.NormaliseBasePath(value));

    private static (string BasePath, string? PublicOrigin) Config(string name)
    {
        var c = Golden()["configs"]![name]!;
        return (c["basePath"]!.GetValue<string>(), c["publicOrigin"]?.GetValue<string>());
    }

    private static List<KeyValuePair<string, string>> Headers(JsonNode response) =>
        response["headers"]!.AsObject().Select(h => KeyValuePair.Create(h.Key, h.Value!.GetValue<string>())).OrderBy(h => h.Key, StringComparer.Ordinal).ToList();

    private static KitHost Host(string basePath, string? publicOrigin)
    {
        var corpora = new CorpusDirectory(RepoLayout.Behaviours, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitHost(new KitRouter(corpora, viewer, new UiBundle(FixtureDist), null), new UrlParser(), Serialiser, basePath, publicOrigin);
    }

    private static async Task<(int Status, Dictionary<string, string> Headers, string Text)> Send(int port, string method, string target, string? origin)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);
        var stream = tcp.GetStream();
        var request = $"{method} {target} HTTP/1.1\r\nHost: localhost\r\n" + (origin is null ? string.Empty : $"Origin: {origin}\r\n") + "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.Latin1.GetBytes(request));

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var split = IndexOf(bytes, "\r\n\r\n"u8.ToArray());
        var head = Encoding.Latin1.GetString(bytes, 0, split).Split("\r\n");
        var headers = head.Skip(1).Select(l => l.Split(':', 2)).ToDictionary(p => p[0].ToLowerInvariant(), p => p[1].Trim());
        var body = bytes[(split + 4)..];
        if (headers.TryGetValue("transfer-encoding", out var te) && te == "chunked")
        {
            body = Dechunk(body);
        }

        return (int.Parse(head[0].Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), headers, Encoding.UTF8.GetString(body));
    }

    private static byte[] Dechunk(byte[] b)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (true)
        {
            var eol = IndexOf(b[i..], "\r\n"u8.ToArray()) + i;
            var size = Convert.ToInt32(Encoding.ASCII.GetString(b, i, eol - i), 16);
            if (size == 0)
            {
                return output.ToArray();
            }

            output.Write(b, eol + 2, size);
            i = eol + 2 + size + 2;
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static JsonNode Golden() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "host.json")))!;

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);
}
