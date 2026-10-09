using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// The GitHub App installation credential (kit#88, process.md's 2026-09-30 override): signs a
/// JWT with the App's private key, exchanges it for an installation token, and keeps that token
/// until five minutes before GitHub says it expires — an expired token is re-minted, never
/// returned as a failed request ("some similar refresh token mechanism to what you are using").
/// </summary>
/// <param name="http">Sent the exchange; tests hand it a fake handler.</param>
/// <param name="appId">The App's id, the JWT's <c>iss</c>.</param>
/// <param name="installationId">Which installation the token is for.</param>
/// <param name="privateKeyPem">PKCS#1 or PKCS#8 PEM, as GitHub downloads it. Never written into a reason.</param>
/// <param name="clock">Default <see cref="TimeProvider.System"/>.</param>
/// <param name="api">Default <c>https://api.github.com/</c>.</param>
public sealed class GitHubAppTokenSource : IGitHubTokenSource, IDisposable
{
    private static readonly Uri DefaultApi = new("https://api.github.com/");

    // GitHub refuses an iat in its future, so the JWT is backdated a minute for clock drift,
    // and lives ten minutes in all — the most GitHub accepts.
    private static readonly TimeSpan Backdate = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan JwtLife = TimeSpan.FromMinutes(9);
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    private readonly HttpClient http;
    private readonly string appId;
    private readonly string installationId;
    private readonly RSA key;
    private readonly TimeProvider clock;
    private readonly Uri api;

    // One exchange at a time: two Commits arriving together on a cold cache must not mint twice.
    private readonly SemaphoreSlim gate = new(1, 1);
    // A reference, not a tuple: it is read outside the lock, and a struct this size can tear.
    private volatile Minted? cached;

    public GitHubAppTokenSource(HttpClient http, string appId, string installationId, string privateKeyPem, TimeProvider? clock = null, Uri? api = null)
    {
        this.http = http;
        this.appId = appId.Trim();
        this.installationId = installationId.Trim();
        this.clock = clock ?? TimeProvider.System;
        this.api = api ?? DefaultApi;
        key = RSA.Create();
        try
        {
            key.ImportFromPem(privateKeyPem);
        }
        catch (ArgumentException e)
        {
            // Thrown at startup, by design: a key that cannot be read is a deployment fault, and
            // finding it on the first Commit would be finding it on his phone.
            key.Dispose();
            throw new GitHubTokenException("KIT_GITHUB_PRIVATE_KEY is not a PEM RSA private key", e);
        }
    }

    /// <inheritdoc />
    public async Task<string?> TokenAsync(CancellationToken cancellationToken = default)
    {
        if (Fresh() is { } token)
        {
            return token;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-read under the lock: whoever held it before us may have just minted one.
            return Fresh() ?? await MintAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private string? Fresh() =>
        cached is { } c && clock.GetUtcNow() < c.ExpiresAt - RefreshBefore ? c.Token : null;

    private async Task<string> MintAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(api, $"app/installations/{Uri.EscapeDataString(installationId)}/access_tokens"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Jwt());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("kit", "1"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new GitHubTokenException($"GitHub could not be reached to mint an installation token ({e.Message})", e);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubTokenException("GitHub did not answer in time when minting an installation token", e);
        }

        using (response)
        {
            var code = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                // The status alone: GitHub's message for a bad JWT can echo what it was sent.
                throw new GitHubTokenException($"GitHub answered {code} when minting an installation token for app {appId}, installation {installationId}");
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var json = JsonDocument.Parse(text);
                if (json.RootElement is { ValueKind: JsonValueKind.Object } o
                    && o.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } token
                    && o.TryGetProperty("expires_at", out var x) && x.ValueKind == JsonValueKind.String && x.TryGetDateTimeOffset(out var expiresAt))
                {
                    cached = new Minted(token, expiresAt);
                    return token;
                }
            }
            catch (JsonException)
            {
                // Falls through: unreadable is the same sentence as the wrong shape.
            }

            throw new GitHubTokenException($"GitHub answered {code} with a body that is not an installation token");
        }
    }

    private string Jwt()
    {
        var now = clock.GetUtcNow();
        var header = Base64Url("""{"alg":"RS256","typ":"JWT"}"""u8);
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = (now - Backdate).ToUnixTimeSeconds(),
            exp = (now + JwtLife).ToUnixTimeSeconds(),
            iss = appId,
        }));
        var signed = $"{header}.{claims}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signed}.{Base64Url(signature)}";
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record Minted(string Token, DateTimeOffset ExpiresAt)
    {
        // A record prints its members, and this one holds a live token.
        public override string ToString() => $"installation token, expires {ExpiresAt:O}";
    }

    public void Dispose()
    {
        key.Dispose();
        gate.Dispose();
    }
}
