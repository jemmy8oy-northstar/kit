using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IPullRequestOpener"/> over GitHub's REST API (kit#147). Two calls at most: find
/// an open pull request from the head into the base, and only if there is none, create one —
/// so pressing Commit twice proposes the work once.
/// </summary>
/// <param name="http">Sent every request; tests hand it a fake handler.</param>
/// <param name="token">
/// <c>KIT_GIT_TOKEN</c> — the same credential the image pushes with (kit#144). Sent as a
/// header and never written into a reason, because a reason is shown in the UI.
/// </param>
/// <param name="repository"><c>owner/name</c>, usually <see cref="RepositoryFromRemote"/> of the clone's <c>origin</c>.</param>
/// <param name="api">Default <c>https://api.github.com/</c>.</param>
public sealed partial class GitHubPullRequestOpener(
    HttpClient http,
    string? token,
    string? repository,
    Uri? api = null) : IPullRequestOpener
{
    private static readonly Uri DefaultApi = new("https://api.github.com/");

    /// <summary>
    /// <c>owner/name</c> from a GitHub remote URL in any of the forms <c>git remote get-url</c>
    /// prints (https, with or without credentials or <c>.git</c>; scp-style and <c>ssh://</c>),
    /// or null for anything that is not github.com — a guess here would open a pull request
    /// against the wrong repository.
    /// </summary>
    public static string? RepositoryFromRemote(string? url)
    {
        var m = RemotePattern().Match((url ?? string.Empty).Trim());
        return m.Success ? $"{m.Groups["owner"].Value}/{m.Groups["name"].Value}" : null;
    }

    /// <inheritdoc />
    public async Task<IPullRequestResult> OpenAsync(string head, string baseBranch, string title, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PullRequestResult { Reason = "no GitHub token is set (KIT_GIT_TOKEN), so Kit cannot open a pull request" };
        }

        if (string.IsNullOrEmpty(repository) || repository.Split('/') is not [{ Length: > 0 } owner, { Length: > 0 }])
        {
            return new PullRequestResult { Reason = "Kit does not know which GitHub repository its clone came from, so it cannot open a pull request" };
        }

        var root = new Uri(api ?? DefaultApi, $"repos/{repository}/pulls");

        // GitHub matches `head` only in the owner:branch form; a bare branch name finds nothing and
        // would open a duplicate on every press.
        var query = $"?state=open&head={Uri.EscapeDataString($"{owner}:{head}")}&base={Uri.EscapeDataString(baseBranch)}";
        var found = await SendAsync(HttpMethod.Get, new Uri(root + query), null, cancellationToken);
        if (found.Reason is not null)
        {
            return new PullRequestResult { Reason = found.Reason };
        }

        if (found.Json is not { ValueKind: JsonValueKind.Array } list)
        {
            return new PullRequestResult { Reason = NotAPullRequest(found.Status) };
        }

        if (list.GetArrayLength() > 0)
        {
            return Identify(list[0]) is var (number, url)
                ? new PullRequestResult { AlreadyOpen = true, Number = number, Url = url }
                : new PullRequestResult { Reason = NotAPullRequest(found.Status) };
        }

        var payload = JsonSerializer.Serialize(new { title, head, @base = baseBranch, body });
        var created = await SendAsync(HttpMethod.Post, root, payload, cancellationToken);
        if (created.Reason is not null)
        {
            return new PullRequestResult { Reason = created.Reason };
        }

        return Identify(created.Json!.Value) is var (n, u)
            ? new PullRequestResult { Opened = true, Number = n, Url = u }
            : new PullRequestResult { Reason = NotAPullRequest(created.Status) };
    }

    // A 2xx whose JSON is the wrong SHAPE is a sentence like every other outcome, never a throw:
    // reading a property off a non-object threw, and Commit answered an unhandled 500 (kit#160's blind review).
    private static string NotAPullRequest(int status) => $"GitHub answered {status} with JSON that is not a pull request";

    /// <summary>The number and page of a pull request object, or null when it is not one.</summary>
    private static (int Number, string Url)? Identify(JsonElement pr) =>
        pr.ValueKind == JsonValueKind.Object
        && pr.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var i)
        && pr.TryGetProperty("html_url", out var u) && u.ValueKind == JsonValueKind.String
            ? (i, u.GetString()!)
            : null;

    // One request, and which LAYER failed if it did: unreachable, slow, refused, or unreadable
    // need different fixes, the same split GitStore.Git makes for git (kit#39).
    private async Task<(JsonElement? Json, string? Reason, int Status)> SendAsync(HttpMethod method, Uri uri, string? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);

        // Trimmed: a secret created from a file ends in a newline, and .NET throws a FormatException
        // for one in a header value — outside every catch below (kit#160's blind review).
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("kit", "1"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (payload is not null)
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            return (null, $"GitHub could not be reached ({e.Message})", 0);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, "GitHub did not answer in time", 0);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement? json = null;
            try
            {
                json = text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : null;
            }
            catch (JsonException)
            {
                // Falls through: a success with an unreadable body is still unreadable.
            }

            var code = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"GitHub answered {code}: {Explain(response.StatusCode, json)}", code);
            }

            return json is null
                ? (null, $"GitHub answered {code} with a body Kit could not read", code)
                : (json, null, code);
        }
    }

    // GitHub's `message` is generic ("Validation Failed"); the useful sentence is usually in
    // errors[0].message ("No commits between dev and kit/hosted"), so both are kept.
    private static string Explain(HttpStatusCode status, JsonElement? json)
    {
        var parts = new List<string>();
        if (json is { ValueKind: JsonValueKind.Object } o)
        {
            if (o.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            {
                parts.Add(m.GetString()!);
            }

            if (o.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in errs.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var em) && em.ValueKind == JsonValueKind.String)
                    {
                        parts.Add(em.GetString()!);
                    }
                }
            }
        }

        return parts.Count > 0 ? string.Join(" — ", parts) : status.ToString();
    }

    [GeneratedRegex(@"^(?:https://(?:[^@/]+@)?github\.com/|(?:ssh://)?git@github\.com[:/])(?<owner>[A-Za-z0-9-]+)/(?<name>[A-Za-z0-9._-]+?)(?:\.git)?/?$")]
    private static partial Regex RemotePattern();
}
