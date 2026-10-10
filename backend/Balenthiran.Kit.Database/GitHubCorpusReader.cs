using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="IGitHubCorpusReader"/> over GitHub's REST API (kit#88, BEH-PULL-1..3). One
/// contents listing per read, conditional on the last ETag, then one blob per file whose sha
/// moved. Blobs are fetched by sha, so the text is the text at that listing even if the branch
/// moves mid-read.
/// </summary>
/// <param name="http">Sent every request; tests hand it a fake handler.</param>
/// <param name="tokens">
/// The App installation or <c>KIT_GIT_TOKEN</c>; null from it means read without one, which a
/// public repository allows (BEH-PULL-2). Asked per read, so an expired token is replaced.
/// </param>
/// <param name="api">Default <c>https://api.github.com/</c>.</param>
public sealed class GitHubCorpusReader(HttpClient http, IGitHubTokenSource tokens, Uri? api = null) : IGitHubCorpusReader
{
    private static readonly Uri DefaultApi = new("https://api.github.com/");

    // `readFileSync(f, 'utf8')`, as CorpusDirectory.ReadText: a BOM is kept, so a splice
    // written back through the clone changes only the line it meant to.
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <inheritdoc />
    public async Task<ICorpusSnapshot> ReadAsync(IProjectSource source, ICorpusSnapshot? previous = null, CancellationToken cancellationToken = default)
    {
        // A snapshot of a different source would hand its ETag to the wrong directory and be
        // answered 304 — another repository's corpora, served as this one's.
        if (previous is not null && Key(previous.Source) != Key(source))
        {
            previous = null;
        }

        // BEH-PULL-2: "a public read that fails because a token could not be minted is a bug". So a
        // failed mint reads WITHOUT a token, as a public repository allows, and the mint's reason is
        // kept for the one answer it explains — a 404, which is also how GitHub hides a private repo.
        string? token;
        string? mintFailed = null;
        try
        {
            token = await tokens.TokenAsync(cancellationToken);
        }
        catch (GitHubTokenException e)
        {
            token = null;
            mintFailed = e.Message;
        }

        var root = api ?? DefaultApi;
        var listing = new Uri(root, $"repos/{source.Owner}/{source.Repository}/contents/{Escape(source.Path)}?ref={Uri.EscapeDataString(source.Branch)}");
        using var answer = await SendAsync(listing, token, previous?.ETag, "application/vnd.github+json", source, mintFailed, cancellationToken);
        if (answer.StatusCode == HttpStatusCode.NotModified && previous is not null)
        {
            return previous;
        }

        var entries = Entries(await ReadBodyAsync(answer, source, cancellationToken), source);
        var files = new SortedDictionary<string, ISnapshotFile>(StringComparer.Ordinal);
        foreach (var (name, sha) in entries)
        {
            if (previous is not null && previous.Files.TryGetValue(name, out var kept) && kept.Sha == sha)
            {
                files[name] = kept;
                continue;
            }

            var blob = new Uri(root, $"repos/{source.Owner}/{source.Repository}/git/blobs/{sha}");
            using var raw = await SendAsync(blob, token, null, "application/vnd.github.raw+json", source, mintFailed, cancellationToken);
            var bytes = await raw.Content.ReadAsByteArrayAsync(cancellationToken);
            files[name] = new SnapshotFile { Sha = sha, Text = Utf8.GetString(bytes) };
        }

        return new CorpusSnapshot { Source = source, ETag = answer.Headers.ETag?.ToString(), Files = files };
    }

    /// <summary>The corpus files of a contents listing: <c>.beh</c> and <c>.bindings.json</c> files directly in it, nothing nested.</summary>
    private static List<(string Name, string Sha)> Entries(JsonElement json, IProjectSource source)
    {
        // A FILE at the path answers an object, not an array: the source names something that is
        // not a directory, which is a configuration error and not "no projects".
        if (json.ValueKind != JsonValueKind.Array)
        {
            throw new GitHubReadException($"{Key(source)}: GitHub did not answer a directory listing — is the path a directory?");
        }

        var entries = new List<(string, string)>();
        foreach (var e in json.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object
                || !e.TryGetProperty("type", out var type) || type.GetString() != "file"
                || !e.TryGetProperty("name", out var n) || n.GetString() is not { } name
                || !(name.EndsWith(".beh", StringComparison.Ordinal) || name.EndsWith(".bindings.json", StringComparison.Ordinal)))
            {
                continue;
            }

            // A listed corpus with no sha cannot be fetched; skipping it would serve fewer projects
            // than exist, which reads as a deletion.
            if (!e.TryGetProperty("sha", out var s) || s.GetString() is not { Length: > 0 } sha)
            {
                throw new GitHubReadException($"{Key(source)}: GitHub listed {name} with no sha, so Kit cannot fetch it");
            }

            entries.Add((name, sha));
        }

        return entries;
    }

    // One request, and which LAYER failed if it did, as GitHubPullRequestOpener names it.
    private async Task<HttpResponseMessage> SendAsync(Uri uri, string? token, string? etag, string accept, IProjectSource source, string? mintFailed, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(token))
        {
            // Trimmed: a secret created from a file ends in a newline, and .NET throws for one in a header.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("kit", "1"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new GitHubReadException($"{Key(source)}: GitHub could not be reached ({e.Message})", e);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubReadException($"{Key(source)}: GitHub did not answer in time", e);
        }

        if (response.IsSuccessStatusCode || (response.StatusCode == HttpStatusCode.NotModified && etag is not null))
        {
            return response;
        }

        using (response)
        {
            var code = (int)response.StatusCode;
            var message = await MessageAsync(response, cancellationToken);

            // 404 is also what GitHub answers a PRIVATE repository read without a token (BEH-PULL-3),
            // so the sentence says both rather than guess which.
            var hint = response.StatusCode != HttpStatusCode.NotFound || !string.IsNullOrWhiteSpace(token) ? string.Empty
                : mintFailed is not null ? $" — the repository, branch or path does not exist, or it is private and the App token could not be minted: {mintFailed}"
                : " — the repository, branch or path does not exist, or it is private and no GitHub token is set";
            throw new GitHubReadException($"{Key(source)}: GitHub answered {code}: {message}{hint}");
        }
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response, IProjectSource source, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new GitHubReadException($"{Key(source)}: GitHub answered {(int)response.StatusCode} with a body Kit could not read", e);
        }
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            {
                return m.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Falls through to the status name.
        }

        return response.StatusCode.ToString();
    }

    /// <summary><c>owner/repo@branch:path</c>, the form <c>KIT_PROJECTS</c> names it in — and the only identity a snapshot is matched on.</summary>
    private static string Key(IProjectSource s) => $"{s.Owner}/{s.Repository}@{s.Branch}:{s.Path}";

    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
