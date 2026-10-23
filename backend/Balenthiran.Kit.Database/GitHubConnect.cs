using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// Connects an early-access user's GitHub (BEH-USERS-1) through Kit's GitHub App: exchanges the
/// code GitHub hands back after the user authorises the App for a user access token, and asks
/// GitHub who that user is and whether the App is installed on their own account. The token is
/// used for those two questions and revoked; nothing of it is kept or written into a reason.
/// </summary>
/// <param name="http">Sent both requests; tests hand it a fake handler.</param>
/// <param name="slug">The App's name in its URL, <c>KIT_GITHUB_APP_SLUG</c>.</param>
/// <param name="clientId">The App's OAuth client id, <c>KIT_GITHUB_CLIENT_ID</c>.</param>
/// <param name="clientSecret">The App's OAuth client secret, <c>KIT_GITHUB_CLIENT_SECRET</c>. Never written into a reason.</param>
/// <param name="redirectUri">Where GitHub sends the user back to: Kit's page, registered on the App as its callback URL.</param>
/// <param name="operatorInstallation">The installation Kit itself runs on (<c>KIT_GITHUB_INSTALLATION_ID</c>): his, so never a user's.</param>
/// <param name="web">Default <c>https://github.com/</c>.</param>
/// <param name="api">Default <c>https://api.github.com/</c>.</param>
public sealed class GitHubConnect(
    HttpClient http,
    string slug,
    string clientId,
    string clientSecret,
    string redirectUri,
    string? operatorInstallation = null,
    Uri? web = null,
    Uri? api = null) : IGitHubConnect
{
    private readonly Uri web = web ?? new Uri("https://github.com/");
    private readonly Uri api = api ?? new Uri("https://api.github.com/");

    /// <inheritdoc />
    public string InstallUrl => new Uri(web, $"apps/{Uri.EscapeDataString(slug)}/installations/new").ToString();

    /// <inheritdoc />
    public string AuthorizeUrl(string state) => new Uri(web,
        $"login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}").ToString();

    /// <inheritdoc />
    public async Task<IGitHubLink?> InstallationAsync(string code, CancellationToken cancellationToken = default)
    {
        var token = await ExchangeAsync(code, cancellationToken);
        try
        {
            using var user = await SendAsync(AsUser(HttpMethod.Get, "user", token), "reading who you are", cancellationToken);
            if (user.RootElement is not { ValueKind: JsonValueKind.Object } u
                || !u.TryGetProperty("login", out var l) || l.GetString() is not { Length: > 0 } login)
            {
                throw new GitHubConnectException("GitHub's answer about who you are was not in the shape Kit reads");
            }

            // Lists only this App's installations, but every one the user can REACH — an
            // organisation's included, which any member reaches. Only their own account's is theirs.
            using var json = await SendAsync(AsUser(HttpMethod.Get, "user/installations?per_page=100", token), "listing your installations", cancellationToken);
            if (json.RootElement is not { ValueKind: JsonValueKind.Object } o
                || !o.TryGetProperty("installations", out var all) || all.ValueKind != JsonValueKind.Array)
            {
                throw new GitHubConnectException("GitHub's list of your installations was not in the shape Kit reads");
            }

            foreach (var i in all.EnumerateArray())
            {
                if (i.TryGetProperty("id", out var id) && id.TryGetInt64(out var installation)
                    && i.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object
                    && account.TryGetProperty("login", out var owner) && string.Equals(owner.GetString(), login, StringComparison.OrdinalIgnoreCase)
                    && installation.ToString(System.Globalization.CultureInfo.InvariantCulture) != operatorInstallation?.Trim())
                {
                    return new GitHubInstallation(installation, login);
                }
            }

            return null;
        }
        finally
        {
            await RevokeAsync(token);
        }
    }

    private HttpRequestMessage AsUser(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(api, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    /// <summary>
    /// Ends the user token now rather than when it expires, hours later. Best effort and silent:
    /// the connection is already decided, and a failure here leaves only a token nobody holds.
    /// </summary>
    private async Task RevokeAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(api, $"applications/{Uri.EscapeDataString(clientId)}/token"))
        {
            Content = JsonContent(new { access_token = token }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("kit", "1"));
        try
        {
            using var _ = await http.SendAsync(request, CancellationToken.None);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
        }
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");

    private async Task<string> ExchangeAsync(string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(web, "login/oauth/access_token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var json = await SendAsync(request, "confirming your authorisation", cancellationToken);
        if (json.RootElement is { ValueKind: JsonValueKind.Object } o
            && o.TryGetProperty("access_token", out var t) && t.GetString() is { Length: > 0 } token)
        {
            return token;
        }

        // GitHub answers a spent or wrong code 200 with an error field; its name is safe to show.
        var error = json.RootElement is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty("error", out var err) ? err.GetString() : null;
        throw new GitHubConnectException(error == "bad_verification_code"
            ? "GitHub refused the authorisation code: it was used already or has expired, so connect again"
            : $"GitHub did not confirm the authorisation ({error ?? "no token in its answer"})");
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, string doing, CancellationToken cancellationToken)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("kit", "1"));
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new GitHubConnectException($"GitHub could not be reached when {doing} ({e.Message})", e);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubConnectException($"GitHub did not answer in time when {doing}", e);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // The status alone: a body could echo what it was sent.
                throw new GitHubConnectException($"GitHub answered {(int)response.StatusCode} when {doing}");
            }

            try
            {
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException e)
            {
                throw new GitHubConnectException($"GitHub answered {doing} with a body that is not JSON", e);
            }
        }
    }
}
