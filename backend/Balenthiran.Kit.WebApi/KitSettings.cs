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

    /// <summary>
    /// <c>KIT_DATABASE</c>, the Npgsql connection string of the database early-access accounts live
    /// in (BEH-USERS-1). Unset means accounts are off. WRITE-ONLY for the same reason as
    /// <see cref="GitToken"/>: it carries a password.
    /// </summary>
    public string? Database
    {
        init => database = value;
    }

    private readonly string? database;

    /// <summary>The connection string, for the user store only.</summary>
    internal string? ConnectionString() => database;

    /// <summary>True when accounts are on.</summary>
    public bool Accounts => database is not null;

    /// <summary>
    /// <c>KIT_GITHUB_APP_SLUG</c> and <c>KIT_GITHUB_CLIENT_ID</c>: with the secret below, how an
    /// early-access user connects their GitHub (BEH-USERS-1). All three or none, and only with
    /// accounts and a public origin, which GitHub sends the user back to.
    /// </summary>
    public string? GitHubAppSlug { get; init; }

    /// <inheritdoc cref="GitHubAppSlug" />
    public string? GitHubClientId { get; init; }

    /// <summary><c>KIT_GITHUB_CLIENT_SECRET</c>. WRITE-ONLY for the same reason as <see cref="GitToken"/>.</summary>
    public string? GitHubClientSecret
    {
        init => gitHubClientSecret = value;
    }

    private readonly string? gitHubClientSecret;

    /// <summary>The client secret, for the connector only.</summary>
    internal string? ClientSecret() => gitHubClientSecret;

    /// <summary>True when users can connect their GitHub; <see cref="FromEnvironment"/> has already refused a partial set.</summary>
    public bool GitHubConnect => GitHubClientId is not null;

    /// <summary>Kit's page, which GitHub sends a user back to after they authorise the App; register it as the App's callback URL.</summary>
    public string? GitHubRedirect => PublicOrigin is null ? null : $"{PublicOrigin.TrimEnd('/')}{BasePath}/";

    /// <summary><c>KIT_OPERATOR</c>: the name the operator signs in with when accounts are on; default <c>james</c>.</summary>
    public string Operator { get; init; } = KitRouter.DefaultOperator;

    /// <summary>True when the App credential is set; <see cref="FromEnvironment"/> has already refused a partial one.</summary>
    public bool GitHubApp => GitHubAppId is not null;

    /// <summary><c>owner/name</c> of the repository the edits branch lives in, from <c>KIT_GIT_CLONE</c>; null for anything not on github.com.</summary>
    public string? GitRepository { get; init; }

    /// <summary><c>KIT_GIT_OFF_REASON</c> (kit#233): why the entrypoint left write-back off, repeated in the deployed warning.</summary>
    public string? GitOffReason { get; init; }

    /// <summary><c>KIT_GIT_BASE</c>: what Commit proposes the edits into — the branch the entrypoint starts the edits branch from.</summary>
    public string GitBase { get; init; } = "dev";

    /// <summary>
    /// <c>KIT_PROJECTS</c> (kit#88, BEH-PULL-1): where the corpora are READ from on GitHub, as
    /// <c>owner/repo@branch:path</c> separated by commas or whitespace. Empty means read the
    /// directory on disk, as before — a laptop, a test, or a pod that has not opted in.
    /// </summary>
    public IReadOnlyList<ProjectSource> Projects { get; init; } = [];

    /// <summary>
    /// <c>KIT_PROJECTS_POLL</c>, seconds a read of <see cref="Projects"/> is served before the next
    /// request starts another (kit#300, BEH-PULL-5); default 120. Idle, Kit reads nothing.
    /// A re-read of an unchanged directory is one conditional request, but WITHOUT a token even a
    /// 304 spends one of GitHub's 60 an hour, so the default stays well under that per source.
    /// </summary>
    public TimeSpan ProjectsPoll { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// <c>KIT_AGENT_FEED</c> (BEH-NOTIFY-1): the agent's notifications file, as
    /// <c>owner/repo@branch:path</c> with the path naming a file. Unset means no feed, and the
    /// notifications page says so. Read every <see cref="ProjectsPoll"/>, like the projects.
    /// </summary>
    public ProjectSource? AgentFeed { get; init; }

    /// <summary>
    /// <c>KIT_AGENT_SCHEDULE</c> (BEH-NOTIFY-5): the agent's pause file, <c>state/suspend.json</c>
    /// in its repo, as <c>owner/repo@branch:path</c>. Unset means the notifications page says it
    /// cannot tell when the agent next wakes. Read with the feed.
    /// </summary>
    public ProjectSource? AgentSchedule { get; init; }

    /// <summary>
    /// <c>KIT_PUSH_PUBLIC_KEY</c> (BEH-NOTIFY-3): the VAPID public key from the optional <c>kit-push</c>
    /// secret, base64url. Public by design: the browser subscribes with it. Unset means push is off.
    /// </summary>
    public string? PushPublicKey { get; init; }

    /// <summary><c>KIT_PUSH_PRIVATE_KEY</c>, base64url. WRITE-ONLY for the same reason as <see cref="GitToken"/>.</summary>
    public string? PushPrivateKey
    {
        init => pushPrivateKey = value;
    }

    private readonly string? pushPrivateKey;

    /// <summary>The key, for the push sender only.</summary>
    internal string? PushKey() => pushPrivateKey;

    /// <summary><c>KIT_PUSH_SUBJECT</c>: the contact a push service may reach, a <c>mailto:</c> or https URL (VAPID's <c>sub</c>).</summary>
    public string PushSubject { get; init; } = DefaultPushSubject;

    /// <summary>What <see cref="PushSubject"/> is when <c>KIT_PUSH_SUBJECT</c> is unset.</summary>
    public const string DefaultPushSubject = "mailto:kit@balenthiran.co.uk";

    /// <summary>True when both VAPID keys are set; <see cref="FromEnvironment"/> has already refused a half-set pair.</summary>
    public bool Push => PushPublicKey is not null;

    /// <summary>
    /// <c>KIT_ANTHROPIC_API_KEY</c> (BEH-SPECAGENT-4), from the optional <c>kit-anthropic</c> secret he
    /// created on 2026-11-02. WRITE-ONLY for the same reason as <see cref="GitToken"/>. Unset means the Inbox offers no chat.
    /// </summary>
    public string? AnthropicApiKey
    {
        init => anthropicApiKey = value;
    }

    private readonly string? anthropicApiKey;

    /// <summary>The key, for the triage chat only.</summary>
    internal string? AnthropicKey() => anthropicApiKey;

    /// <summary>True when the spec agent can be talked to.</summary>
    public bool Triage => anthropicApiKey is not null;

    /// <summary><c>KIT_TRIAGE_DAILY_LIMIT</c>: spec-agent replies a day, the spend cap. Unset is <see cref="KitRouter.DefaultTriageLimit"/>.</summary>
    public int TriageLimit { get; init; } = KitRouter.DefaultTriageLimit;

    /// <summary><c>KIT_TRIAGE_DAILY_LIMIT</c>: a whole number, 0 or more (0 switches replies off); unset is the default. Anything else THROWS.</summary>
    public static int ParseTriageLimit(string? value) =>
        string.IsNullOrWhiteSpace(value) ? KitRouter.DefaultTriageLimit
        : int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n
        : throw new InvalidOperationException($"KIT_TRIAGE_DAILY_LIMIT is \"{value}\": set it to a whole number of replies a day, 0 or more");

    /// <summary><c>KIT_AGENT_RUN_CAP_USD</c> (BEH-USERS-9): what one agent run may spend through the model proxy, USD. Unset is <see cref="AgentProxy.DefaultCapUsd"/>.</summary>
    public decimal AgentRunCapUsd { get; init; } = AgentProxy.DefaultCapUsd;

    /// <summary><c>KIT_AGENT_RUN_CAP_USD</c>: a decimal number of dollars, 0 or more (0 stops every run's first call); unset is the default. Anything else THROWS.</summary>
    public static decimal ParseAgentRunCap(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AgentProxy.DefaultCapUsd
        : decimal.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n
        : throw new InvalidOperationException($"KIT_AGENT_RUN_CAP_USD is \"{value}\": set it to a number of dollars a run may spend, 0 or more, such as 5 or 2.50");

    /// <summary><c>KIT_AGENT_RUN_MAX_HOURS</c> (BEH-USERS-9): how long after its creation a run's token reaches the model. Unset is <see cref="AgentProxy.DefaultMaxAge"/>.</summary>
    public TimeSpan AgentRunMaxAge { get; init; } = AgentProxy.DefaultMaxAge;

    /// <summary><c>KIT_AGENT_RUN_MAX_HOURS</c>: a number of hours, above 0 (fractions allowed); unset is the default. Anything else THROWS.</summary>
    public static TimeSpan ParseAgentRunMaxAge(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AgentProxy.DefaultMaxAge
        : double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 0 && h <= 24 * 365 ? TimeSpan.FromHours(h)
        : throw new InvalidOperationException($"KIT_AGENT_RUN_MAX_HOURS is \"{value}\": set it to the hours a run's token works for, above 0, such as 6 or 0.5");

    /// <summary><c>KIT_RUNNER_POOL_TOKEN</c> (BEH-USERS-9): what runner pods claim queued runs with. Null or blank means there is no pool.</summary>
    public string? RunnerPoolToken
    {
        init => runnerPoolToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private readonly string? runnerPoolToken;

    /// <summary>The pool token, for the claim route and nothing else: never logged or echoed.</summary>
    internal string? RunnerPoolSecret() => runnerPoolToken;

    /// <summary>True when runner pods may claim runs.</summary>
    public bool RunnerPool => runnerPoolToken is not null;

    /// <summary><c>KIT_RUNNER_CLAIM_WAIT_SECONDS</c>: how long a claim waits for work before answering 204. Unset is <see cref="RunnerPoolRoutes.DefaultClaimWait"/>.</summary>
    public TimeSpan RunnerClaimWait { get; init; } = RunnerPoolRoutes.DefaultClaimWait;

    /// <summary><c>KIT_RUNNER_CLAIM_WAIT_SECONDS</c>: seconds, 0 to 120; unset is the default. Anything else THROWS.</summary>
    public static TimeSpan ParseRunnerClaimWait(string? value) =>
        string.IsNullOrWhiteSpace(value) ? RunnerPoolRoutes.DefaultClaimWait
        : double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var s) && s >= 0 && s <= 120 ? TimeSpan.FromSeconds(s)
        : throw new InvalidOperationException($"KIT_RUNNER_CLAIM_WAIT_SECONDS is \"{value}\": set it to a number of seconds from 0 to 120, such as 25");

    /// <summary><c>KIT_STAGE_MODEL_TRIAGE</c> (BEH-ORCH-2): the model the triage stage runs on. Unset is <see cref="StageModels.DefaultTriage"/>.</summary>
    public string StageModelTriage { get; init; } = StageModels.DefaultTriage;

    /// <summary><c>KIT_STAGE_MODEL_TRIAGE</c>: a model name; unset is the default. Anything else THROWS.</summary>
    public static string ParseStageModelTriage(string? value) => StageModels.Parse("KIT_STAGE_MODEL_TRIAGE", value, StageModels.DefaultTriage);

    /// <summary><c>KIT_STAGE_MODEL_REVIEW</c> (BEH-ORCH-2): the model that reviews a flagged stage. Unset is <see cref="StageModels.DefaultReview"/>.</summary>
    public string StageModelReview { get; init; } = StageModels.DefaultReview;

    /// <summary><c>KIT_STAGE_MODEL_REVIEW</c>: a model name; unset is the default. Anything else THROWS.</summary>
    public static string ParseStageModelReview(string? value) => StageModels.Parse("KIT_STAGE_MODEL_REVIEW", value, StageModels.DefaultReview);

    /// <summary><c>KIT_STAGE_MODEL_REVIEW_LOW</c> (BEH-ORCH-2): the model that reviews a flag marked "(level: low)". Unset is <see cref="StageModels.DefaultReviewLow"/>.</summary>
    public string StageModelReviewLow { get; init; } = StageModels.DefaultReviewLow;

    /// <summary><c>KIT_STAGE_MODEL_REVIEW_LOW</c>: a model name; unset is the default. Anything else THROWS.</summary>
    public static string ParseStageModelReviewLow(string? value) => StageModels.Parse("KIT_STAGE_MODEL_REVIEW_LOW", value, StageModels.DefaultReviewLow);

    /// <summary><c>KIT_STAGE_MODEL_SPEC</c> (BEH-ORCH-3): the model the spec stage runs on. Unset is <see cref="StageModels.DefaultWork"/>.</summary>
    public string StageModelSpec { get; init; } = StageModels.DefaultWork;

    /// <summary><c>KIT_STAGE_MODEL_SPEC</c>: a model name; unset is the default. Anything else THROWS.</summary>
    public static string ParseStageModelSpec(string? value) => StageModels.Parse("KIT_STAGE_MODEL_SPEC", value, StageModels.DefaultWork);

    /// <summary><c>KIT_STAGE_MODEL_BUILD</c> (BEH-ORCH-3): the model the build stage runs on. Unset is <see cref="StageModels.DefaultWork"/>.</summary>
    public string StageModelBuild { get; init; } = StageModels.DefaultWork;

    /// <summary><c>KIT_STAGE_MODEL_BUILD</c>: a model name; unset is the default. Anything else THROWS.</summary>
    public static string ParseStageModelBuild(string? value) => StageModels.Parse("KIT_STAGE_MODEL_BUILD", value, StageModels.DefaultWork);

    /// <summary><c>KIT_STAGE_TURNS_TRIAGE</c> (BEH-ORCH-5): the turn limit of a triage run. Unset is <see cref="StageTurnSettings.DefaultTriage"/>.</summary>
    public int StageTurnsTriage { get; init; } = StageTurnSettings.DefaultTriage;

    /// <summary><c>KIT_STAGE_TURNS_TRIAGE</c>: a whole number above zero; unset is the default. Anything else THROWS.</summary>
    public static int ParseStageTurnsTriage(string? value) => StageTurnSettings.Parse("KIT_STAGE_TURNS_TRIAGE", value, StageTurnSettings.DefaultTriage);

    /// <summary><c>KIT_STAGE_TURNS_REVIEW</c> (BEH-ORCH-5): the turn limit of a review run. Unset is <see cref="StageTurnSettings.DefaultReview"/>.</summary>
    public int StageTurnsReview { get; init; } = StageTurnSettings.DefaultReview;

    /// <summary><c>KIT_STAGE_TURNS_REVIEW</c>: a whole number above zero; unset is the default. Anything else THROWS.</summary>
    public static int ParseStageTurnsReview(string? value) => StageTurnSettings.Parse("KIT_STAGE_TURNS_REVIEW", value, StageTurnSettings.DefaultReview);

    /// <summary><c>KIT_STAGE_TURNS_SPEC</c> (BEH-ORCH-5): the turn limit of a spec run. Unset is <see cref="StageTurnSettings.DefaultSpec"/>.</summary>
    public int StageTurnsSpec { get; init; } = StageTurnSettings.DefaultSpec;

    /// <summary><c>KIT_STAGE_TURNS_SPEC</c>: a whole number above zero; unset is the default. Anything else THROWS.</summary>
    public static int ParseStageTurnsSpec(string? value) => StageTurnSettings.Parse("KIT_STAGE_TURNS_SPEC", value, StageTurnSettings.DefaultSpec);

    /// <summary><c>KIT_STAGE_TURNS_BUILD</c> (BEH-ORCH-5): the turn limit of a build run. Unset is <see cref="StageTurnSettings.DefaultBuild"/>.</summary>
    public int StageTurnsBuild { get; init; } = StageTurnSettings.DefaultBuild;

    /// <summary><c>KIT_STAGE_TURNS_BUILD</c>: a whole number above zero; unset is the default. Anything else THROWS.</summary>
    public static int ParseStageTurnsBuild(string? value) => StageTurnSettings.Parse("KIT_STAGE_TURNS_BUILD", value, StageTurnSettings.DefaultBuild);

    /// <summary>
    /// <c>KIT_PIPELINE</c> (BEH-ORCH-3): <c>on</c> makes a note left in Kit queue a triage run on the runner pool,
    /// which spends Kit's own Anthropic credit. Unset or <c>off</c> is off: nothing runs.
    /// </summary>
    public bool Pipeline { get; init; }

    /// <summary><c>KIT_PIPELINE</c>: <c>on</c> or <c>off</c>; unset is off. Anything else THROWS, so a typo is not silently off.</summary>
    public static bool ParsePipeline(string? value) => value?.Trim() switch
    {
        null or "" or "off" => false,
        "on" => true,
        _ => throw new InvalidOperationException($"KIT_PIPELINE is \"{value}\": set it to on or off"),
    };

    /// <summary>
    /// <c>KIT_PIPELINE_PROJECTS</c>: the only projects whose notes the pipeline triages, comma-separated; unset is every
    /// project. A triage pushes bundles quoting his notes to the project's repo, and most project repos are public
    /// (kit#714).
    /// </summary>
    public IReadOnlySet<string>? PipelineProjects { get; init; }

    /// <summary><c>KIT_PIPELINE_PROJECTS</c>: null when unset or blank; else project names. THROWS for a malformed list.</summary>
    public static IReadOnlySet<string>? ParsePipelineProjects(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var names = value.Split(',', StringSplitOptions.TrimEntries);
        return names.All(n => ProjectName.IsMatch(n))
            ? names.ToHashSet(StringComparer.Ordinal)
            : throw new InvalidOperationException($"KIT_PIPELINE_PROJECTS is \"{value}\": set it to project names separated by commas");
    }

    private static readonly System.Text.RegularExpressions.Regex ProjectName = new("^[a-z0-9]+(-[a-z0-9]+)*$");

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

        var database = env("KIT_DATABASE") is { } db && !string.IsNullOrWhiteSpace(db) ? db : null;
        if (database is not null && string.IsNullOrWhiteSpace(env("KIT_PASSWORD")))
        {
            // Accounts sit behind the lock: with no operator password there is no one to create them.
            throw new InvalidOperationException("KIT_DATABASE is set but KIT_PASSWORD is not: accounts need the operator's password, so set both, or unset KIT_DATABASE");
        }

        var connect = new[] { "KIT_GITHUB_APP_SLUG", "KIT_GITHUB_CLIENT_ID", "KIT_GITHUB_CLIENT_SECRET" }
            .ToDictionary(n => n, n => env(n) is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null);
        var unset = connect.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList();
        if (unset.Count is > 0 and < 3)
        {
            throw new InvalidOperationException($"connecting a user's GitHub is half set: {string.Join(" and ", unset)} missing — set all three, or none");
        }

        if (unset.Count == 0 && (database is null || string.IsNullOrWhiteSpace(env("KIT_PUBLIC_ORIGIN"))))
        {
            // A connection belongs to an account, and GitHub needs a public page to send the user back to.
            throw new InvalidOperationException("KIT_GITHUB_CLIENT_ID is set but KIT_DATABASE or KIT_PUBLIC_ORIGIN is not: a user's GitHub is connected to their account, on Kit's public page, so set both, or unset the three KIT_GITHUB_APP_SLUG/CLIENT_ID/CLIENT_SECRET");
        }

        var push = new[] { "KIT_PUSH_PUBLIC_KEY", "KIT_PUSH_PRIVATE_KEY" }
            .ToDictionary(n => n, n => env(n) is { } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null);
        if (push.Values.Count(v => v is null) == 1)
        {
            throw new InvalidOperationException($"web push is half set: {push.First(kv => kv.Value is null).Key} missing — set both, or none for no push");
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
            GitOffReason = env("KIT_GIT_OFF_REASON") is { Length: > 0 } why ? why : null,
            GitBase = env("KIT_GIT_BASE") is { Length: > 0 } g ? g : "dev",
            GitHubAppId = app["KIT_GITHUB_APP_ID"],
            GitHubInstallationId = app["KIT_GITHUB_INSTALLATION_ID"],
            GitHubPrivateKey = app["KIT_GITHUB_PRIVATE_KEY"],
            Projects = ParseProjects(env("KIT_PROJECTS")),
            ProjectsPoll = ParsePoll(env("KIT_PROJECTS_POLL")),
            AgentFeed = ParseAgentFeed(env("KIT_AGENT_FEED")),
            AgentSchedule = ParseAgentFeed(env("KIT_AGENT_SCHEDULE"), "KIT_AGENT_SCHEDULE"),
            Database = database,
            PushPublicKey = push["KIT_PUSH_PUBLIC_KEY"],
            PushPrivateKey = push["KIT_PUSH_PRIVATE_KEY"],
            PushSubject = env("KIT_PUSH_SUBJECT") is { Length: > 0 } subject ? subject : DefaultPushSubject,
            GitHubAppSlug = connect["KIT_GITHUB_APP_SLUG"],
            GitHubClientId = connect["KIT_GITHUB_CLIENT_ID"],
            GitHubClientSecret = connect["KIT_GITHUB_CLIENT_SECRET"],
            Operator = env("KIT_OPERATOR") is { Length: > 0 } op ? op : KitRouter.DefaultOperator,
            AnthropicApiKey = env("KIT_ANTHROPIC_API_KEY") is { } k && !string.IsNullOrWhiteSpace(k) ? k.Trim() : null,
            TriageLimit = ParseTriageLimit(env("KIT_TRIAGE_DAILY_LIMIT")),
            AgentRunCapUsd = ParseAgentRunCap(env("KIT_AGENT_RUN_CAP_USD")),
            AgentRunMaxAge = ParseAgentRunMaxAge(env("KIT_AGENT_RUN_MAX_HOURS")),
            StageModelTriage = ParseStageModelTriage(env("KIT_STAGE_MODEL_TRIAGE")),
            StageModelReview = ParseStageModelReview(env("KIT_STAGE_MODEL_REVIEW")),
            StageModelReviewLow = ParseStageModelReviewLow(env("KIT_STAGE_MODEL_REVIEW_LOW")),
            StageModelSpec = ParseStageModelSpec(env("KIT_STAGE_MODEL_SPEC")),
            StageModelBuild = ParseStageModelBuild(env("KIT_STAGE_MODEL_BUILD")),
            StageTurnsTriage = ParseStageTurnsTriage(env("KIT_STAGE_TURNS_TRIAGE")),
            StageTurnsReview = ParseStageTurnsReview(env("KIT_STAGE_TURNS_REVIEW")),
            StageTurnsSpec = ParseStageTurnsSpec(env("KIT_STAGE_TURNS_SPEC")),
            StageTurnsBuild = ParseStageTurnsBuild(env("KIT_STAGE_TURNS_BUILD")),
            RunnerPoolToken = env("KIT_RUNNER_POOL_TOKEN"),
            RunnerClaimWait = ParseRunnerClaimWait(env("KIT_RUNNER_CLAIM_WAIT_SECONDS")),
            Pipeline = ParsePipeline(env("KIT_PIPELINE")),
            PipelineProjects = ParsePipelineProjects(env("KIT_PIPELINE_PROJECTS")),
        };
    }

    /// <summary><c>KIT_AGENT_FEED</c> (or <c>KIT_AGENT_SCHEDULE</c>): unset is none; one that does not parse THROWS, as a KIT_PROJECTS entry does.</summary>
    public static ProjectSource? ParseAgentFeed(string? value, string name = "KIT_AGENT_FEED") =>
        string.IsNullOrWhiteSpace(value) ? null
        : ProjectSource.Parse(value) ?? throw new InvalidOperationException($"{name} \"{value}\" is not owner/repo@branch:path");

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
