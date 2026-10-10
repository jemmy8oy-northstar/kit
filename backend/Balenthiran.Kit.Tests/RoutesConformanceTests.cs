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

    /// <summary>The committed fixture bundle the golden's <c>dist</c> requests were served from.</summary>
    private static string FixtureDist => Path.Combine(RepoLayout.Conformance, "routes", "dist");

    public static TheoryData<string, string, bool> Requests()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var r in Golden()["requests"]!.AsArray())
        {
            data.Add(r!["method"]!.GetValue<string>(), r["path"]!.GetValue<string>(), r["dist"]?.GetValue<bool>() ?? false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public void The_router_answers_as_ui_js_does(string method, string path, bool dist)
    {
        var expected = Expected(method, path, dist);
        var response = Router(dist ? FixtureDist : Path.Combine(RepoLayout.Root, "no-bundle-here")).Route(method, path);
        var actual = JsonNode.Parse(Serialiser.Serialise(response))!;

        // Bytes are scored as the golden records them: UTF-8 text, after the other keys.
        if (response.Raw is not null)
        {
            actual["rawText"] = System.Text.Encoding.UTF8.GetString(response.Raw);
        }

        // Status first, so a failure says WHICH answer differs before diffing bodies.
        Assert.Equal(expected["status"]!.GetValue<int>(), actual["status"]!.GetValue<int>());
        Assert.Equal(Canonical(expected), Canonical(actual));
    }

    /// <summary>
    /// Node's <c>path.extname</c>, measured: the "names a file" test that decides
    /// between a JSON 404 and the shell. Leading dots are not an extension; <c>...</c> is.
    /// </summary>
    [Theory]
    [InlineData("/a/b.js", ".js")]
    [InlineData("/a/.b", "")]
    [InlineData("/a/b.", ".")]
    [InlineData("/..", "")]
    [InlineData("/a/..x", ".x")]
    [InlineData("/a/.b.c", ".c")]
    [InlineData("/assets", "")]
    [InlineData("/", "")]
    [InlineData("/a.b/c", "")]
    [InlineData("/...", ".")]
    [InlineData("/a/b.JS", ".JS")]
    [InlineData("/a.b/", ".b")]
    public void Extname_matches_Node(string path, string expected) =>
        Assert.Equal(expected, UiBundle.Extname(path));

    /// <summary>
    /// The host delivers bundle BYTES and the cache header, not a JSON rendering of them:
    /// the shell for a client-side route, and a hashed asset cached forever.
    /// </summary>
    [Fact]
    public async Task The_host_delivers_the_bundle_as_bytes_with_its_cache_header()
    {
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], new KitSettings(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot, null, FixtureDist));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient();

        using var shell = await http.GetAsync(address + "/projects/snip-it");
        Assert.Equal("text/html; charset=utf-8", shell.Content.Headers.ContentType!.ToString());
        Assert.Equal("no-store", shell.Headers.CacheControl!.ToString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(FixtureDist, "index.html")), await shell.Content.ReadAsByteArrayAsync());

        using var asset = await http.GetAsync(address + "/assets/index-Ab12Cd.js");
        Assert.Equal("public, max-age=31536000, immutable", asset.Headers.CacheControl!.ToString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(FixtureDist, "assets", "index-Ab12Cd.js")), await asset.Content.ReadAsByteArrayAsync());
        await app.StopAsync();
    }

    /// <summary>No bundle is a 503 that says how to build one — never a 404 for every path.</summary>
    [Fact]
    public void With_no_bundle_every_page_says_it_has_not_been_built()
    {
        var r = Router(Path.Combine(RepoLayout.Root, "no-bundle-here")).Route("GET", "/projects/snip-it");
        Assert.Equal(503, r.Status);
        Assert.Contains("The Kit UI has not been built", System.Text.Encoding.UTF8.GetString(r.Raw!), StringComparison.Ordinal);
        Assert.Equal(200, Router(Path.Combine(RepoLayout.Root, "no-bundle-here")).Route("GET", "/api/health").Status);
    }

    /// <summary>
    /// BEH-UI-4: coverage the list could not read is <c>null</c>, never zero. With no repos given the
    /// list cannot measure anything, so every row must say so rather than report an empty list — a UI
    /// that cannot tell "no mapping exists" from "nothing is covered" renders the second.
    /// </summary>
    [Fact]
    public void Unavailable_coverage_is_null_in_the_list_never_zero()
    {
        var list = JsonNode.Parse(Serialiser.Serialise(Router(Path.Combine(RepoLayout.Root, "no-bundle-here")).Route("GET", "/api/projects")))!;
        var projects = list["body"]!["projects"]!.AsArray();
        Assert.NotEmpty(projects);
        foreach (var p in projects)
        {
            var coverage = p!["coverage"]!;
            Assert.False(coverage["available"]!.GetValue<bool>(), $"{p["app"]}: coverage claims to be available with no repos");
            Assert.Null(coverage["covered"]);
            Assert.Null(coverage["uncovered"]);
            Assert.False(string.IsNullOrEmpty(coverage["reason"]?.GetValue<string>()), $"{p["app"]}: unavailable coverage must say why");
        }
    }

    /// <summary>The golden is only a score if it holds what it claims: every corpus, and every refusal.</summary>
    [Fact]
    public void The_golden_covers_every_corpus_and_every_refusal()
    {
        var paths = Golden()["requests"]!.AsArray().Select(r => r!["path"]!.GetValue<string>()).ToHashSet();
        var corpora = Directory.GetFiles(RepoLayout.FrozenBehaviours, "*.beh").Select(Path.GetFileNameWithoutExtension).ToList();
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
        var settings = new KitSettings(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot, null, FixtureDist);
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
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], new KitSettings(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot, null, FixtureDist));
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
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        string Session(string? password) => JsonSerializer.Serialize(
            new KitRouter(corpora, Viewer(corpora), new UiBundle(FixtureDist), password).Route("GET", "/api/session").Body, Serialiser.Options);

        Assert.Contains("\"required\": false", Session(" \ufeff\u2028"), StringComparison.Ordinal);
        Assert.Contains("\"required\": true", Session(" x "), StringComparison.Ordinal);

        // U+0085 is .NET whitespace and NOT JavaScript's, so it is a real password.
        Assert.Contains("\"required\": true", Session("\u0085"), StringComparison.Ordinal);
    }

    private static JsonNode Golden() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoLayout.Conformance, "routes", "read.json")))!;

    private static JsonNode Expected(string method, string path, bool dist = false) =>
        Golden()["requests"]!.AsArray().Single(r => r!["method"]!.GetValue<string>() == method && r["path"]!.GetValue<string>() == path
            && (r["dist"]?.GetValue<bool>() ?? false) == dist)!["response"]!;

    private static KitRouter Router(string dist)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        return new KitRouter(corpora, Viewer(corpora), new UiBundle(dist), null);
    }

    private static ProjectViewer Viewer(CorpusDirectory corpora) =>
        new(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());

    private static string Canonical(JsonNode? node) => JsonSerializer.Serialize(node, Serialiser.Options);
}
