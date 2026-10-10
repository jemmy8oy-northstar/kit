using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-PULL-3 (kit#88): a private project is read against a LIVE installation token — the App's key
/// exchanged for one, never sent as one, and re-minted once it expires rather than failing the read.
/// Through the server's own wiring (settings → services → directory → reader), against a scripted
/// GitHub that throws on any request it was not told to expect.
/// </summary>
public class PullAuthTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_private_project_is_read_with_the_installation_token_and_an_expired_one_is_re_minted()
    {
        var clock = new Clock(Start);
        var gh = new FakeGitHub(
            Minted("ghs_first", Start.AddHours(1)),
            Listed("""[{"type": "file", "name": "secret.beh", "sha": "s1"}]""", "W/\"e1\""),
            Raw("behaviour BEH-S \"s\"\n"),
            Minted("ghs_second", Start.AddHours(3)),
            _ => new HttpResponseMessage(HttpStatusCode.NotModified));
        var env = new Dictionary<string, string>
        {
            ["KIT_GITHUB_APP_ID"] = "123",
            ["KIT_GITHUB_INSTALLATION_ID"] = "456",
            ["KIT_GITHUB_PRIVATE_KEY"] = RSA.Create(2048).ExportRSAPrivateKeyPem(),
            ["KIT_PROJECTS"] = "o/private@dev:behaviours",
        };
        var services = new ServiceCollection();
        services.AddSingleton<HttpMessageHandler>(gh);
        services.AddSingleton<TimeProvider>(clock);
        services.AddKitServices(KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root));
        await using var sp = services.BuildServiceProvider();
        var dir = sp.GetRequiredService<GitHubCorpusDirectory>();

        await dir.RefreshAsync();
        Assert.Equal(["secret"], dir.Corpora());

        clock.Advance(TimeSpan.FromHours(2)); // past ghs_first's expiry
        await dir.RefreshAsync();

        Assert.Equal(
            [
                ("POST", "/app/installations/456/access_tokens", "Bearer"),
                ("GET", "/repos/o/private/contents/behaviours", "Bearer ghs_first"),
                ("GET", "/repos/o/private/git/blobs/s1", "Bearer ghs_first"),
                ("POST", "/app/installations/456/access_tokens", "Bearer"),
                ("GET", "/repos/o/private/contents/behaviours", "Bearer ghs_second"),
            ],
            gh.Sent.Select(s => (s.Method.Method, s.Uri.AbsolutePath, s.Method == HttpMethod.Post ? s.Authorization!.Split(' ')[0] : s.Authorization!)));

        // The key is exchanged, never sent: the mint carries a JWT, and no read carries anything but a minted token.
        Assert.All(gh.Sent.Where(s => s.Method == HttpMethod.Post), s => Assert.Equal(3, s.Authorization!["Bearer ".Length..].Split('.').Length));
        Assert.Equal(["secret"], dir.Corpora());
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Minted(string token, DateTimeOffset expires) =>
        FakeGitHub.Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { token, expires_at = expires.ToString("O") }));

    private static Func<HttpRequestMessage, HttpResponseMessage> Listed(string json, string etag) => _ =>
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        r.Headers.ETag = System.Net.Http.Headers.EntityTagHeaderValue.Parse(etag);
        return r;
    };

    private static Func<HttpRequestMessage, HttpResponseMessage> Raw(string text) => _ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(text)) };

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
