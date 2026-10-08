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
            env("KIT_GIT_BRANCH") is { Length: > 0 } b ? b : null);
    }

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
