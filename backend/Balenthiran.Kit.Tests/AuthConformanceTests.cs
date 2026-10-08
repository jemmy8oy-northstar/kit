using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The write gates' score: every scenario in <c>conformance/routes/auth.json</c> — sign-in,
/// the throttle, expiry, sign-out, the lock, the loopback rule and the CSRF check, recorded
/// from <c>ui.js</c>'s <c>answer()</c> + <c>received()</c> on a fake clock — replayed through
/// <see cref="KitHost"/> on the same clock, and then over a real Kestrel socket.
/// </summary>
public class AuthConformanceTests
{
    private const long Epoch = 1_700_000_000_000;

    private static readonly EngineJsonSerialiser Serialiser = new();

    private static readonly HashSet<string> TransportOwn = ["date", "connection", "keep-alive", "content-length", "transfer-encoding"];

    public static TheoryData<int> Scenarios()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Golden()["scenarios"]!.AsArray().Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Every_step_answers_as_ui_js_does(int index)
    {
        var s = Golden()["scenarios"]![index]!;
        var clock = new Clock();
        var host = Host(s["config"]!, clock);

        foreach (var step in s["steps"]!.AsArray())
        {
            if (step!["advance"] is { } advance)
            {
                clock.T += advance.GetValue<long>();
                continue;
            }

            var label = $"{s["name"]}: {step["method"]} {step["path"]} cookie={step["cookie"]} body={step["body"]}";
            var origin = step["origin"]?.GetValue<string>();
            var cookie = step["cookie"]?.GetValue<string>();
            var a = host.Answer(step["method"]!.GetValue<string>(), step["path"]!.GetValue<string>(), origin, cookie);
            if (a.Post is not null)
            {
                a = host.Received(a.Post, Body(step), origin, cookie);
            }

            var expected = step["response"]!;
            Assert.True(expected["status"]!.GetValue<int>() == a.Status, $"{label}: status {a.Status}, body {a.Body}");
            Assert.Equal(Headers(expected), a.Headers.OrderBy(h => h.Key, StringComparer.Ordinal).ToList());
            Assert.Equal(Canonical(expected["body"]), Canonical(JsonNode.Parse(a.Body!)));
        }
    }

    /// <summary>
    /// The far side: each scenario against a real server whose session store and throttle
    /// share the test's fake clock, bodies sent as raw bytes and <c>tooLarge</c> as
    /// <c>maxBody + 1</c> of them, so the size limit is the one Kestrel's read enforces.
    /// </summary>
    [Fact]
    public async Task Every_scenario_replays_over_a_real_socket()
    {
        var mismatches = new List<string>();
        var sent = 0;
        foreach (var s in Golden()["scenarios"]!.AsArray())
        {
            var clock = new Clock();
            var tokens = 0;
            var config = s!["config"]!;
            var settings = new KitSettings(RepoLayout.Behaviours, RepoLayout.Root, config["password"]?.GetValue<string>(), Path.Combine(RepoLayout.Root, "no-bundle-here"), string.Empty, config["publicOrigin"]?.GetValue<string>(), config["host"]!.GetValue<string>());
            await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, services =>
            {
                services.AddSingleton<ISessionStore>(new SessionStore(() => clock.T, () => $"tok-{++tokens}"));
                services.AddSingleton<ISignInThrottle>(new SignInThrottle(() => clock.T));
            });
            await app.StartAsync();
            var port = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First()).Port;

            foreach (var step in s["steps"]!.AsArray())
            {
                if (step!["advance"] is { } advance)
                {
                    clock.T += advance.GetValue<long>();
                    continue;
                }

                var label = $"{s["name"]}: {step["method"]} {step["path"]} cookie={step["cookie"]} body={step["body"]}";
                var raw = step["tooLarge"]?.GetValue<bool>() == true ? new byte[Golden()["maxBody"]!.GetValue<int>() + 1] : Body(step);
                var (status, headers, text) = await Send(port, step["method"]!.GetValue<string>(), step["path"]!.GetValue<string>(), step["origin"]?.GetValue<string>(), step["cookie"]?.GetValue<string>(), raw);
                sent++;

                var expected = step["response"]!;
                var want = Headers(expected).ToDictionary(h => h.Key, h => h.Value);
                var extra = headers.Keys.Where(h => !TransportOwn.Contains(h) && !want.ContainsKey(h)).ToList();
                var wrong = want.Where(h => !headers.TryGetValue(h.Key, out var v) || v != h.Value).Select(h => h.Key).ToList();
                var bodyOk = text.Length > 0 && Canonical(expected["body"]) == Canonical(JsonNode.Parse(text));
                if (status != expected["status"]!.GetValue<int>() || extra.Count > 0 || wrong.Count > 0 || !bodyOk)
                {
                    mismatches.Add($"{label}: status {status} (want {expected["status"]}), extra [{string.Join(", ", extra)}], wrong [{string.Join(", ", wrong)}], body {text[..Math.Min(100, text.Length)]}");
                }
            }

            await app.StopAsync();
        }

        var recorded = Golden()["scenarios"]!.AsArray().SelectMany(s => s!["steps"]!.AsArray()).Count(st => st!["advance"] is null);
        Assert.True(sent == recorded && sent >= 40, $"{sent} of {recorded} steps went over the wire — the gate is inert");
        Assert.True(mismatches.Count == 0, string.Join('\n', mismatches));
    }

    /// <summary>The throttle's doubling is capped, so it can never lock its owner out for good.</summary>
    [Fact]
    public void The_throttle_cooldown_is_capped()
    {
        var clock = new Clock();
        var throttle = new SignInThrottle(() => clock.T);
        for (var i = 0; i < 200; i++)
        {
            throttle.Fail();
        }

        Assert.Equal(SignInThrottle.CooldownMaxMs, throttle.RetryAfterMs());
    }

    /// <summary>The default token: 32 bytes from the CSPRNG, as 64 hex characters, never repeated.</summary>
    [Fact]
    public void A_default_token_is_64_hex_characters_and_unique()
    {
        var store = new SessionStore();
        var a = store.Create();
        var b = store.Create();
        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.NotEqual(a, b);
    }

    private static KitHost Host(JsonNode config, Clock clock)
    {
        var tokens = 0;
        var corpora = new CorpusDirectory(RepoLayout.Behaviours, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var publicOrigin = config["publicOrigin"]?.GetValue<string>();
        var policy = new OriginPolicy(new UrlParser(), publicOrigin);
        var router = new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            config["password"]?.GetValue<string>(),
            policy,
            new SessionStore(() => clock.T, () => $"tok-{++tokens}"),
            new SignInThrottle(() => clock.T),
            config["host"]!.GetValue<string>(),
            publicOrigin is not null && publicOrigin.StartsWith("https:", StringComparison.OrdinalIgnoreCase));
        return new KitHost(router, new UrlParser(), Serialiser, policy, string.Empty);
    }

    private static byte[]? Body(JsonNode step) =>
        step["tooLarge"]?.GetValue<bool>() == true ? null : step["body"] is { } b ? Encoding.UTF8.GetBytes(b.GetValue<string>()) : [];

    private static List<KeyValuePair<string, string>> Headers(JsonNode response) =>
        response["headers"]!.AsObject().Select(h => KeyValuePair.Create(h.Key, h.Value!.GetValue<string>())).OrderBy(h => h.Key, StringComparer.Ordinal).ToList();

    private static async Task<(int Status, Dictionary<string, string> Headers, string Text)> Send(int port, string method, string target, string? origin, string? cookie, byte[]? body)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);
        var stream = tcp.GetStream();
        var head = new StringBuilder($"{method} {target} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n");
        if (origin is not null)
        {
            head.Append($"Origin: {origin}\r\n");
        }

        if (cookie is not null)
        {
            head.Append($"Cookie: {cookie}\r\n");
        }

        if (body is not null && method == "POST")
        {
            head.Append($"Content-Length: {body.Length}\r\n");
        }

        head.Append("\r\n");
        await stream.WriteAsync(Encoding.Latin1.GetBytes(head.ToString()));
        if (body is not null && method == "POST")
        {
            await stream.WriteAsync(body);
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var split = IndexOf(bytes, "\r\n\r\n"u8.ToArray());
        var lines = Encoding.Latin1.GetString(bytes, 0, split).Split("\r\n");
        var headers = lines.Skip(1).Select(l => l.Split(':', 2)).ToDictionary(p => p[0].ToLowerInvariant(), p => p[1].Trim());
        var payload = bytes[(split + 4)..];
        if (headers.TryGetValue("transfer-encoding", out var te) && te == "chunked")
        {
            payload = Dechunk(payload);
        }

        return (int.Parse(lines[0].Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), headers, Encoding.UTF8.GetString(payload));
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
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "auth.json")))!;

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);

    private sealed class Clock
    {
        public long T { get; set; } = Epoch;
    }
}
