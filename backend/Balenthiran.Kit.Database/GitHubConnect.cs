using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// Connects an early-access user's GitHub (BEH-USERS-1) through Kit's GitHub App: exchanges the
/// code GitHub hands back after the user authorises the App for a user access token, and asks
/// GitHub which of the App's installations that user can reach. The token is used for that one
/// question and dropped; nothing of it is kept or written into a reason.
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
    public async Task<IReadOnlyList<IGitHubLink>> InstallationsAsync(string code, CancellationToken cancellationToken = default)
    {
        var token = await ExchangeAsync(code, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(api, "user/installations?per_page=100"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var json = await SendAsync(request, "listing your installations", cancellationToken);
        if (json.RootElement is not { ValueKind: JsonValueKind.Object } o
            || !o.TryGetProperty("installations", out var all) || all.ValueKind != JsonValueKind.Array)
        {
            throw new GitHubConnectException("GitHub's list of your installations was not in the shape Kit reads");
        }

        var links = new List<IGitHubLink>();
        foreach (var i in all.EnumerateArray())
        {
            if (i.TryGetProperty("id", out var id) && id.TryGetInt64(out var installation)
                && i.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object
                && account.TryGetProperty("login", out var login) && login.GetString() is { Length: > 0 } name
                && installation.ToString(System.Globalization.CultureInfo.InvariantCulture) != operatorInstallation?.Trim())
            {
                links.Add(new GitHubInstallation(installation, name));
            }
        }

        return links;
    }

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
