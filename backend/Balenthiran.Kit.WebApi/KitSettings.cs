using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// What the server is pointed at, read from the same environment <c>ui.js</c> reads.
/// <c>RepoRoot</c> is the Kit checkout; every <c>corpus</c> path in a view is relative to it.
/// <c>BasePath</c> is <c>KIT_BASE_PATH</c>, normalised; <c>PublicOrigin</c> is <c>KIT_PUBLIC_ORIGIN</c>.
/// <c>Host</c> is the bind address the loopback write rule asks about — <c>ui.js</c>'s
/// <c>--host</c>, read here from <c>KIT_HOST</c> (default loopback, as there). <c>Git</c>,
/// <c>GitRemote</c> and <c>GitBranch</c> are <c>--git</c>, <c>--git-remote</c> and
/// <c>--git-branch</c>, read from <c>KIT_GIT</c>, <c>KIT_GIT_REMOTE</c> and <c>KIT_GIT_BRANCH</c>.
/// </summary>
public sealed record KitSettings(
    string Dir,
    string RepoRoot,
    string? Password,
    string Dist,
    string BasePath = "",
    string? PublicOrigin = null,
    string Host = "127.0.0.1",
    bool Git = false,
    string? GitRemote = null,
    string? GitBranch = null)
{
    /// <summary>
    /// <c>KIT_GIT_TOKEN</c>, which Commit opens its pull request with (kit#155) — the credential the
    /// entrypoint already pushes with. ⚠️ WRITE-ONLY on purpose, read through <see cref="Token"/>: a
    /// record's <c>ToString</c> prints every property it can read, so a readable one would put the
    /// token in any log line that ever formats the settings. An <c>internal get</c> is NOT enough —
    /// the synthesised printer is in this assembly and printed it (CommitRouteTests caught it).
    /// </summary>
    public string? GitToken
    {
        init => gitToken = value;
    }

    private readonly string? gitToken;

    /// <summary>The token, for the pull-request opener only.</summary>
    internal string? Token() => gitToken;

    /// <summary>
    /// <c>KIT_GITHUB_APP_ID</c> and <c>KIT_GITHUB_INSTALLATION_ID</c>: with the key below, the GitHub
    /// App installation process.md settles on (kit#88). All three or none — <see cref="FromEnvironment"/>
    /// throws for a partial set, because quietly falling back to <c>KIT_GIT_TOKEN</c> would hide the
    /// half-configured credential until a Commit failed on his phone.
    /// </summary>
    public string? GitHubAppId { get; init; }

    /// <inheritdoc cref="GitHubAppId" />
    public string? GitHubInstallationId { get; init; }

    /// <summary><c>KIT_GITHUB_PRIVATE_KEY</c>, PEM. WRITE-ONLY for the same reason as <see cref="GitToken"/>.</summary>
    public string? GitHubPrivateKey
    {
        init => gitHubPrivateKey = value;
    }

    private readonly string? gitHubPrivateKey;

    /// <summary>The key, for the token source only.</summary>
    internal string? PrivateKey() => gitHubPrivateKey;

    /// <summary>True when the App credential is set; <see cref="FromEnvironment"/> has already refused a partial one.</summary>
    public bool GitHubApp => GitHubAppId is not null;

    /// <summary><c>owner/name</c> of the repository the edits branch lives in, from <c>KIT_GIT_CLONE</c>; null for anything not on github.com.</summary>
    public string? GitRepository { get; init; }

    /// <summary><c>KIT_GIT_BASE</c>: what Commit proposes the edits into — the branch the entrypoint starts the edits branch from.</summary>
    public string GitBase { get; init; } = "dev";

    /// <summary>
    /// <c>KIT_PROJECTS</c> (kit#88, BEH-PULL-1): where the corpora are READ from on GitHub, as
    /// <c>owner/repo@branch:path</c> separated by commas or whitespace. Empty means read the
    /// directory on disk, as before — a laptop, a test, or a pod that has not opted in.
    /// </summary>
    public IReadOnlyList<ProjectSource> Projects { get; init; } = [];

    /// <summary>
    /// <c>KIT_PROJECTS_POLL</c>, seconds between re-reads of <see cref="Projects"/>; default 120.
    /// A re-read of an unchanged directory is one conditional request, but WITHOUT a token even a
    /// 304 spends one of GitHub's 60 an hour, so the default stays well under that per source.
    /// </summary>
    public TimeSpan ProjectsPoll { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>As <c>parseArgs</c> derives it: an https public origin sets the cookie's <c>Secure</c> flag.</summary>
    public bool Secure => PublicOrigin is not null && PublicOrigin.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// From the environment. <c>KIT_DIR</c> overrides the corpus directory; otherwise
    /// the checkout is found by walking up from the working directory to the one
    /// holding <c>prototypes/behaviour-ast/behaviours</c>, and THROWS if there is none —
    /// a server that silently served an empty list would look like a Kit with no projects.
    /// </summary>
    public static KitSettings FromEnvironment(Func<string, string?> env, string workingDirectory)
    {
        var root = FindRoot(workingDirectory);
        var app = new[] { "KIT_GITHUB_APP_ID", "KIT_GITHUB_INSTALLATION_ID", "KIT_GITHUB_PRIVATE_KEY" }
            .ToDictionary(n => n, n => env(n) is { } v && !string.IsNullOrWhiteSpace(v) ? v : null);
        var missing = app.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
        if (missing.Count is > 0 and < 3)
        {
            throw new InvalidOperationException($"the GitHub App credential is half set: {string.Join(" and ", missing)} missing — set all three, or none to use KIT_GIT_TOKEN");
        }

        var dir = env("KIT_DIR") is { Length: > 0 } d ? Path.GetFullPath(d) : Path.Combine(root, "prototypes", "behaviour-ast", "behaviours");
        return new KitSettings(
            dir,
            root,
            env("KIT_PASSWORD"),
            Path.Combine(root, "prototypes", "behaviour-ast", "ui", "dist"),
            KitHost.NormaliseBasePath(env("KIT_BASE_PATH")),
            env("KIT_PUBLIC_ORIGIN") is { Length: > 0 } o ? o : null,
            env("KIT_HOST") is { Length: > 0 } h ? h : "127.0.0.1",
            GitSwitch(env("KIT_GIT")),
            env("KIT_GIT_REMOTE") is { Length: > 0 } r ? r : null,
            env("KIT_GIT_BRANCH") is { Length: > 0 } b ? b : null)
        {
            GitToken = env("KIT_GIT_TOKEN") is { Length: > 0 } t ? t : null,
            GitRepository = GitHubPullRequestOpener.RepositoryFromRemote(env("KIT_GIT_CLONE")),
            GitBase = env("KIT_GIT_BASE") is { Length: > 0 } g ? g : "dev",
            GitHubAppId = app["KIT_GITHUB_APP_ID"],
            GitHubInstallationId = app["KIT_GITHUB_INSTALLATION_ID"],
            GitHubPrivateKey = app["KIT_GITHUB_PRIVATE_KEY"],
            Projects = ParseProjects(env("KIT_PROJECTS")),
            ProjectsPoll = ParsePoll(env("KIT_PROJECTS_POLL")),
        };
    }

    /// <summary>
    /// <c>KIT_PROJECTS</c>, every entry a <see cref="ProjectSource"/>. One that does not parse
    /// THROWS rather than being skipped: a dropped source is a project that silently vanished.
    /// </summary>
    public static IReadOnlyList<ProjectSource> ParseProjects(string? value)
    {
        var entries = (value ?? string.Empty).Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var sources = entries
            .Select(e => ProjectSource.Parse(e) ?? throw new InvalidOperationException($"KIT_PROJECTS entry \"{e}\" is not owner/repo@branch:path"))
            .ToList();
        var twice = sources.GroupBy(s => s.ToString()).FirstOrDefault(g => g.Count() > 1);
        return twice is null ? sources : throw new InvalidOperationException($"KIT_PROJECTS names {twice.Key} twice");
    }

    /// <summary><c>KIT_PROJECTS_POLL</c>: whole seconds, at least 10; unset is 120. Anything else THROWS.</summary>
    public static TimeSpan ParsePoll(string? value) =>
        string.IsNullOrWhiteSpace(value) ? TimeSpan.FromSeconds(120)
        : int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var s) && s >= 10 ? TimeSpan.FromSeconds(s)
        : throw new InvalidOperationException($"KIT_PROJECTS_POLL is \"{value}\": set it to a whole number of seconds, 10 or more");

    /// <summary>
    /// <c>KIT_GIT</c>: <c>ui.js</c>'s <c>--git</c>. <c>1</c>/<c>true</c> is on; unset, empty,
    /// <c>0</c>/<c>false</c> is off; anything else THROWS — a <c>KIT_GIT=yes</c> read as off would
    /// be a deployed Kit quietly dropping every edit on restart, the failure write-back exists to prevent.
    /// </summary>
    public static bool GitSwitch(string? value) => value switch
    {
        null or "" or "0" => false,
        "1" => true,
        _ when value.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
        _ when value.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
        _ => throw new InvalidOperationException($"KIT_GIT is \"{value}\": set it to 1 or true to commit and push every write, or 0, false or unset for none"),
    };

    private static string FindRoot(string from)
    {
        for (var d = new DirectoryInfo(from); d is not null; d = d.Parent)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "prototypes", "behaviour-ast", "behaviours")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException($"no Kit checkout (prototypes/behaviour-ast/behaviours) at or above {from}");
    }
}
