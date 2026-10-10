using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#88: the GitHub App installation credential is EXCHANGED for a short-lived token and never
/// sent as one, and an expired token is re-minted rather than returned as a failed request.
/// Against a scripted GitHub that throws on any request it was not told to expect — so "did not
/// ask GitHub" is asserted, not assumed.
/// </summary>
public class GitHubAppTokenSourceTests
{
    private const string AppId = "123456";
    private const string InstallationId = "7890";
    private const string Minted = "ghs_minted_never_shown";
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 22, 0, 0, TimeSpan.Zero);

    private static readonly RSA Key = RSA.Create(2048);

    private static Func<HttpRequestMessage, HttpResponseMessage> Token(string token, DateTimeOffset expiresAt) =>
        FakeGitHub.Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { token, expires_at = expiresAt.ToString("O") }));

    private static GitHubAppTokenSource Source(FakeGitHub gh, Clock clock, string? pem = null) =>
        new(new HttpClient(gh), AppId, InstallationId, pem ?? Key.ExportRSAPrivateKeyPem(), clock);

    [Fact]
    public async Task The_key_signs_a_JWT_that_GitHub_exchanges_for_the_installation_token()
    {
        var gh = new FakeGitHub(Token(Minted, Now.AddHours(1)));

        var token = await Source(gh, new Clock(Now)).TokenAsync();

        Assert.Equal(Minted, token);
        var sent = Assert.Single(gh.Sent);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal($"https://api.github.com/app/installations/{InstallationId}/access_tokens", sent.Uri.AbsoluteUri);

        var jwt = sent.Authorization!["Bearer ".Length..].Split('.');
        Assert.Equal(3, jwt.Length);
        Assert.True(
            Key.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), FromBase64Url(jwt[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            "the JWT is not signed RS256 by the App's key");
        using var header = JsonDocument.Parse(FromBase64Url(jwt[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        using var claims = JsonDocument.Parse(FromBase64Url(jwt[1]));
        Assert.Equal(AppId, claims.RootElement.GetProperty("iss").GetString());

        // Backdated a minute for clock drift, ten minutes in all — the most GitHub accepts.
        Assert.Equal(Now.AddSeconds(-60).ToUnixTimeSeconds(), claims.RootElement.GetProperty("iat").GetInt64());
        Assert.Equal(Now.AddMinutes(9).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
    }

    [Fact]
    public async Task A_PKCS8_key_signs_too()
    {
        var gh = new FakeGitHub(Token(Minted, Now.AddHours(1)));

        Assert.Equal(Minted, await Source(gh, new Clock(Now), Key.ExportPkcs8PrivateKeyPem()).TokenAsync());
    }

    [Fact]
    public void A_key_that_is_not_a_PEM_is_refused_when_the_server_starts()
    {
        var e = Assert.Throws<GitHubTokenException>(() => Source(new FakeGitHub(), new Clock(Now), "not a key"));

        Assert.Equal("KIT_GITHUB_PRIVATE_KEY is not a PEM RSA private key", e.Message);
    }

    [Fact]
    public async Task A_live_token_is_reused_without_asking_GitHub_again()
    {
        var gh = new FakeGitHub(Token(Minted, Now.AddHours(1)));
        var clock = new Clock(Now);
        var source = Source(gh, clock);

        await source.TokenAsync();
        clock.Advance(TimeSpan.FromMinutes(54));

        Assert.Equal(Minted, await source.TokenAsync());
        Assert.Single(gh.Sent);
    }

    [Fact]
    public async Task Five_minutes_before_it_expires_a_new_token_is_minted()
    {
        var gh = new FakeGitHub(Token(Minted, Now.AddHours(1)), Token("ghs_second", Now.AddHours(2)));
        var clock = new Clock(Now);
        var source = Source(gh, clock);

        await source.TokenAsync();
        clock.Advance(TimeSpan.FromMinutes(55));

        Assert.Equal("ghs_second", await source.TokenAsync());
        Assert.Equal(2, gh.Sent.Count);
    }

    [Fact]
    public async Task Two_callers_on_a_cold_cache_mint_ONE_token()
    {
        var release = new TaskCompletionSource();
        var gh = new FakeGitHub(_ =>
        {
            release.Task.Wait(TimeSpan.FromSeconds(5));
            return Token(Minted, Now.AddHours(1))(null!);
        });
        var source = Source(gh, new Clock(Now));

        var first = Task.Run(() => source.TokenAsync());
        var second = Task.Run(() => source.TokenAsync());
        await Task.Delay(100);
        release.SetResult();

        Assert.All(await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5)), t => Assert.Equal(Minted, t));
        Assert.Single(gh.Sent);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"A JSON web token could not be decoded"}""", "GitHub answered 401 when minting an installation token for app 123456, installation 7890")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""", "GitHub answered 404 when minting an installation token for app 123456, installation 7890")]
    [InlineData(HttpStatusCode.Created, """{"token":"ghs_x"}""", "GitHub answered 201 with a body that is not an installation token")]
    [InlineData(HttpStatusCode.Created, "<html>proxy</html>", "GitHub answered 201 with a body that is not an installation token")]
    public async Task A_failed_exchange_names_the_status_and_never_the_JWT_or_key(HttpStatusCode status, string body, string reason)
    {
        var gh = new FakeGitHub(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

        var e = await Assert.ThrowsAsync<GitHubTokenException>(() => Source(gh, new Clock(Now)).TokenAsync());

        Assert.Equal(reason, e.Message);
        Assert.DoesNotContain(gh.Sent[0].Authorization!["Bearer ".Length..], e.Message);
    }

    [Fact]
    public async Task An_unreachable_GitHub_is_a_reason_not_a_crash()
    {
        var gh = new FakeGitHub(_ => throw new HttpRequestException("Name or service not known (api.github.com:443)"));

        var e = await Assert.ThrowsAsync<GitHubTokenException>(() => Source(gh, new Clock(Now)).TokenAsync());

        Assert.Equal("GitHub could not be reached to mint an installation token (Name or service not known (api.github.com:443))", e.Message);
    }

    [Fact]
    public async Task The_opener_sends_the_minted_token_and_turns_a_failed_mint_into_a_reason()
    {
        var gh = new FakeGitHub(
            Token(Minted, Now.AddHours(1)),
            FakeGitHub.Json(HttpStatusCode.OK, """[{"number":12,"html_url":"https://github.com/o/k/pull/12"}]"""));
        var opener = new GitHubPullRequestOpener(new HttpClient(gh), Source(gh, new Clock(Now)), "jemmy8oy-northstar/kit");

        var r = await opener.OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.True(r.AlreadyOpen);
        Assert.Equal($"Bearer {Minted}", gh.Sent[1].Authorization);

        var refused = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.Unauthorized, "{}"));
        var failed = await new GitHubPullRequestOpener(new HttpClient(refused), Source(refused, new Clock(Now)), "jemmy8oy-northstar/kit").OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.Equal("GitHub answered 401 when minting an installation token for app 123456, installation 7890", failed.Reason);
        Assert.Single(refused.Sent);
    }

    [Fact]
    public async Task The_registered_server_mints_with_the_App_when_all_three_are_set()
    {
        var settings = KitSettings.FromEnvironment(k => App().GetValueOrDefault(k), RepoLayout.Root);
        var gh = new FakeGitHub(Token(Minted, DateTimeOffset.UtcNow.AddHours(1)));
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s => s.AddSingleton<HttpMessageHandler>(gh));

        var source = app.Services.GetRequiredService<IGitHubTokenSource>();

        Assert.IsType<GitHubAppTokenSource>(source);
        Assert.Equal(Minted, await source.TokenAsync());
        Assert.DoesNotContain("PRIVATE KEY", settings.ToString());
    }

    [Fact]
    public async Task With_none_set_the_static_token_is_used_as_before()
    {
        var env = new Dictionary<string, string?> { ["KIT_GIT_TOKEN"] = "ghp_static\n" };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s => s.AddSingleton<HttpMessageHandler>(new FakeGitHub()));

        var source = app.Services.GetRequiredService<IGitHubTokenSource>();

        Assert.IsType<StaticGitHubTokenSource>(source);
        Assert.Equal("ghp_static", await source.TokenAsync());
    }

    [Theory]
    [InlineData("KIT_GITHUB_APP_ID", "KIT_GITHUB_APP_ID missing")]
    [InlineData("KIT_GITHUB_PRIVATE_KEY", "KIT_GITHUB_PRIVATE_KEY missing")]
    public void A_half_set_App_credential_refuses_to_start_rather_than_fall_back(string unset, string named)
    {
        var env = App();
        env[unset] = "  ";

        var e = Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root));

        Assert.Contains(named, e.Message);
    }

    private static Dictionary<string, string?> App() => new()
    {
        ["KIT_GITHUB_APP_ID"] = AppId,
        ["KIT_GITHUB_INSTALLATION_ID"] = InstallationId,
        ["KIT_GITHUB_PRIVATE_KEY"] = Key.ExportRSAPrivateKeyPem(),
    };

    private static byte[] FromBase64Url(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b.PadRight(b.Length + ((4 - (b.Length % 4)) % 4), '='));
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
