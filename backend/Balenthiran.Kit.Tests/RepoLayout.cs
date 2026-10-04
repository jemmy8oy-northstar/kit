namespace Balenthiran.Kit.Tests;

/// <summary>
/// Where the corpora and the goldens are, found rather than configured.
///
/// The test binary runs from <c>backend/Balenthiran.Kit.Tests/bin/&lt;cfg&gt;/net10.0/</c>, and
/// the fixtures it needs live under <c>prototypes/behaviour-ast/</c>. Copying them
/// into the output directory would make a second copy of the oracle, which is the
/// thing a golden-file harness most needs not to have — so the directory is
/// located by walking up to the repository root.
/// </summary>
internal static class RepoLayout
{
    /// <summary>The corpora: <c>&lt;corpus&gt;.beh</c>.</summary>
    internal static string Behaviours => Path.Combine(Root, "prototypes", "behaviour-ast", "behaviours");

    /// <summary>The committed goldens: <c>&lt;corpus&gt;.json</c>.</summary>
    internal static string Conformance => Path.Combine(Root, "prototypes", "behaviour-ast", "conformance");

    /// <summary>
    /// ⚠️ Throws rather than returning a default. A locator that silently
    /// answered "not found" would give every fixture-reading test an empty
    /// population, and a suite that iterates an empty population reports green —
    /// the failure mode where moving a directory makes the conformance check pass
    /// unconditionally instead of failing loudly.
    /// </summary>
    internal static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        var looked = new List<string>();
        while (d is not null)
        {
            looked.Add(d.FullName);
            if (Directory.Exists(Path.Combine(d.FullName, "prototypes", "behaviour-ast", "conformance")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        throw new DirectoryNotFoundException(
            "cannot look: no prototypes/behaviour-ast/conformance in any parent of "
            + $"{AppContext.BaseDirectory}. Looked in: {string.Join(", ", looked)}");
    }
}
