using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// What the server is pointed at, read from the same environment <c>ui.js</c> reads.
/// <c>RepoRoot</c> is the Kit checkout; every <c>corpus</c> path in a view is relative to it.
/// <c>BasePath</c> is <c>KIT_BASE_PATH</c>, normalised; <c>PublicOrigin</c> is <c>KIT_PUBLIC_ORIGIN</c>.
/// </summary>
public sealed record KitSettings(string Dir, string RepoRoot, string? Password, string Dist, string BasePath = "", string? PublicOrigin = null)
{
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
            env("KIT_PUBLIC_ORIGIN") is { Length: > 0 } o ? o : null);
    }

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
