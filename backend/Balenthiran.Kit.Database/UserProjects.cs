using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// A user's projects, read from their own GitHub (BEH-USERS-2): mints a token for the user's
/// installation of Kit's App, lists the repos the installation reaches, and keeps those whose
/// default branch holds a <c>.beh</c> file. Nothing is stored; every call asks GitHub, so a repo
/// the user removes from the installation, or a corpus they delete, is gone on the next read.
/// </summary>
/// <param name="http">Sent every request; tests hand it a fake handler.</param>
/// <param name="tokens">One token source per installation id — <c>GitHubAppTokenSource</c> in the server, which caches and refreshes each.</param>
/// <param name="api">Default <c>https://api.github.com/</c>.</param>
public sealed class UserProjects(HttpClient http, Func<long, IGitHubTokenSource> tokens, Uri? api = null) : IUserProjects
{
    private const int PerPage = 100;

    // 5,000 repos: past that the list is not one a person chooses a project from, and stopping
    // quietly would show a partial list as the whole of it.
    private const int MaxPages = 50;

    private const int TreeReads = 8;

    private readonly Uri api = api ?? new Uri("https://api.github.com/");

    private readonly ConcurrentDictionary<long, IGitHubTokenSource> sources = new();

    /// <inheritdoc />
    public async Task<IReadOnlyList<IUserProject>> ProjectsAsync(long installation, CancellationToken cancellationToken = default)
    {
        string? token;
        try
        {
            token = await sources.GetOrAdd(installation, tokens).TokenAsync(cancellationToken);
        }
        catch (GitHubTokenException e)
        {
            throw new GitHubReadException(e.Message, e);
        }

        if (token is not { Length: > 0 })
        {
            throw new GitHubReadException("Kit has no GitHub App credential to read your repositories with");
        }

        var repos = await ReposAsync(token, cancellationToken);
        using var gate = new SemaphoreSlim(TreeReads);
        var found = await Task.WhenAll(repos.Select(async r =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return (Repo: r, Corpus: await CorpusAsync(token, r, cancellationToken));
            }
            finally
            {
                gate.Release();
            }
        }));

        return found
            .Where(f => f.Corpus.Count > 0)
            .Select(f => (IUserProject)new Project(f.Repo.Name, f.Repo.FullName, f.Repo.Url, f.Corpus))
            .ToList();
    }

    private async Task<List<Repo>> ReposAsync(string token, CancellationToken cancellationToken)
    {
        var all = new List<Repo>();
        for (var page = 1; page <= MaxPages; page++)
        {
            using var json = await GetAsync(token, $"installation/repositories?per_page={PerPage}&page={page}", "listing your repositories", cancellationToken)
                ?? throw new GitHubReadException("GitHub answered 404 when listing your repositories");
            if (json.RootElement is not { ValueKind: JsonValueKind.Object } o
                || !o.TryGetProperty("repositories", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                throw new GitHubReadException("GitHub's list of your repositories was not in the shape Kit reads");
            }

            var n = 0;
            foreach (var r in list.EnumerateArray())
            {
                n++;
                if (Str(r, "name") is { } name && Str(r, "full_name") is { } full && Str(r, "html_url") is { } url)
                {
                    // An empty repo has no default branch to read; GitHub names none for a branch-less repo.
                    all.Add(new Repo(name, full, url, Str(r, "default_branch")));
                }
                else
                {
                    throw new GitHubReadException("GitHub's list of your repositories was not in the shape Kit reads");
                }
            }

            if (n < PerPage)
            {
                return all;
            }
        }

        throw new GitHubReadException($"you have more than {MaxPages * PerPage} repositories on this installation, more than Kit lists");
    }

    private async Task<List<string>> CorpusAsync(string token, Repo repo, CancellationToken cancellationToken)
    {
        if (repo.DefaultBranch is null)
        {
            return [];
        }

        var path = $"repos/{repo.FullName}/git/trees/{Uri.EscapeDataString(repo.DefaultBranch)}?recursive=1";
        using var json = await GetAsync(token, path, $"reading {repo.FullName}", cancellationToken);

        // GitHub answers 409 for a repository with no commits: no corpus, and nothing wrong.
        if (json is null)
        {
            return [];
        }

        if (json.RootElement is not { ValueKind: JsonValueKind.Object } o
            || !o.TryGetProperty("tree", out var tree) || tree.ValueKind != JsonValueKind.Array)
        {
            throw new GitHubReadException($"GitHub's tree of {repo.FullName} was not in the shape Kit reads");
        }

        // A tree GitHub truncated (about 100,000 entries) still lists what it did return.
        return tree.EnumerateArray()
            .Where(e => Str(e, "type") == "blob" && Str(e, "path") is { } p && p.EndsWith(".beh", StringComparison.Ordinal))
            .Select(e => Str(e, "path")!)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    /// <summary>The parsed answer, or null for the 409 GitHub gives an empty repository.</summary>
    private async Task<JsonDocument?> GetAsync(string token, string path, string doing, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(api, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
            throw new GitHubReadException($"GitHub could not be reached when {doing} ({e.Message})", e);
        }
        catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubReadException($"GitHub did not answer in time when {doing}", e);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict && path.Contains("/git/trees/", StringComparison.Ordinal))
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                // The status alone: a body could echo what it was sent.
                throw new GitHubReadException($"GitHub answered {(int)response.StatusCode} when {doing}");
            }

            try
            {
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException e)
            {
                throw new GitHubReadException($"GitHub answered {doing} with a body that is not JSON", e);
            }
        }
    }

    private sealed record Repo(string Name, string FullName, string Url, string? DefaultBranch);

    private sealed record Project(string Name, string FullName, string Url, IReadOnlyList<string> Corpus) : IUserProject;
}
