using System.Net;
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
/// The C# server's score: every request in <c>conformance/routes/read.json</c>, which
/// <c>conformance.js</c> records from <c>ui.js</c>'s own <c>route()</c> — answered by the
/// C# router, and then delivered over a real socket by the real host, because a handler
/// that returns the right object and a server that delivers it are different claims.
/// </summary>
public class RoutesConformanceTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    public static TheoryData<string, string> Requests()
    {
        var data = new TheoryData<string, string>();
        foreach (var r in Golden()["requests"]!.AsArray())
        {
            data.Add(r!["method"]!.GetValue<string>(), r["path"]!.GetValue<string>());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public void The_router_answers_as_ui_js_does(string method, string path)
    {
        var expected = Expected(method, path);
        var actual = JsonNode.Parse(Serialiser.Serialise(Router().Route(method, path)))!;

        // Status first, so a failure says WHICH answer differs before diffing bodies.
        Assert.Equal(expected["status"]!.GetValue<int>(), actual["status"]!.GetValue<int>());
        Assert.Equal(Canonical(expected), Canonical(actual));
    }

    /// <summary>The golden is only a score if it holds what it claims: every corpus, and every refusal.</summary>
    [Fact]
    public void The_golden_covers_every_corpus_and_every_refusal()
    {
        var paths = Golden()["requests"]!.AsArray().Select(r => r!["path"]!.GetValue<string>()).ToHashSet();
        var corpora = Directory.GetFiles(RepoLayout.Behaviours, "*.beh").Select(Path.GetFileNameWithoutExtension).ToList();
        Assert.True(corpora.Count >= 10, $"only {corpora.Count} corpora found — the scan has stopped matching");
        foreach (var corpus in corpora)
        {
            Assert.Contains($"/api/projects/{corpus}", paths);
        }

        Assert.Contains("/api/projects/%E0%A4%A", paths);
        Assert.Contains("/api/no-such-route", paths);
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/projects")]
    [InlineData("/api/projects/snip-it")]
    [InlineData("/api/projects/no-such-app")]
    [InlineData("/api/projects/%E0%A4%A")]
    public async Task The_host_delivers_what_the_router_answers(string path)
    {
        var expected = Expected("GET", path);
        var settings = new KitSettings(RepoLayout.Behaviours, RepoLayout.Root, null);
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        using var http = new HttpClient();
        using var res = await http.GetAsync(address + path);

        Assert.Equal(expected["status"]!.GetValue<int>(), (int)res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Canonical(expected["body"]), Canonical(JsonNode.Parse(await res.Content.ReadAsStringAsync())));
        await app.StopAsync();
    }

    /// <summary>
    /// The host must hand the router the RAW path. ASP.NET's <c>Request.Path</c> is already
    /// decoded once, so <c>%2541</c> would reach the router as <c>%41</c> and be decoded
    /// AGAIN to <c>A</c> — a double decode no golden request can see, because every one
    /// of them decodes the same either way.
    /// </summary>
    [Fact]
    public async Task The_host_decodes_the_path_exactly_once()
    {
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], new KitSettings(RepoLayout.Behaviours, RepoLayout.Root, null));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        using var http = new HttpClient();
        using var res = await http.GetAsync(address + "/api/projects/%2541");
        var reason = JsonNode.Parse(await res.Content.ReadAsStringAsync())!["reason"]!.GetValue<string>();

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("no corpus named '%41'", reason);
        await app.StopAsync();
    }

    /// <summary>
    /// JavaScript's <c>decodeURIComponent</c>, measured in Node: null where Node throws a URIError.
    /// <c>Uri.UnescapeDataString</c> would return every "THROWS" case unchanged instead.
    /// </summary>
    [Theory]
    [InlineData("%E0%A4%A", null)]
    [InlineData("%2e%2e", "..")]
    [InlineData("%C3%A9", "é")]
    [InlineData("%ED%A0%80", null)]
    [InlineData("%F4%90%80%80", null)]
    [InlineData("%C0%AF", null)]
    [InlineData("%", null)]
    [InlineData("%4", null)]
    [InlineData("abc%20d", "abc d")]
    [InlineData("%E2%82%AC", "€")]
    public void DecodeUriComponent_matches_Node(string input, string? expected) =>
        Assert.Equal(expected, KitRouter.DecodeUriComponent(input));

    [Fact]
    public void A_password_of_only_JavaScript_whitespace_is_no_lock()
    {
        var corpora = new CorpusDirectory(RepoLayout.Behaviours, RepoLayout.Root);
        string Session(string? password) => JsonSerializer.Serialize(
            new KitRouter(corpora, Viewer(corpora), password).Route("GET", "/api/session").Body, Serialiser.Options);

        Assert.Contains("\"required\": false", Session(" \ufeff\u2028"), StringComparison.Ordinal);
        Assert.Contains("\"required\": true", Session(" x "), StringComparison.Ordinal);

        // U+0085 is .NET whitespace and NOT JavaScript's, so it is a real password.
        Assert.Contains("\"required\": true", Session("\u0085"), StringComparison.Ordinal);
    }

    private static JsonNode Golden() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "read.json")))!;

    private static JsonNode Expected(string method, string path) =>
        Golden()["requests"]!.AsArray().Single(r => r!["method"]!.GetValue<string>() == method && r["path"]!.GetValue<string>() == path)!["response"]!;

    private static KitRouter Router()
    {
        var corpora = new CorpusDirectory(RepoLayout.Behaviours, RepoLayout.Root);
        return new KitRouter(corpora, Viewer(corpora), null);
    }

    private static ProjectViewer Viewer(CorpusDirectory corpora) =>
        new(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);
}
