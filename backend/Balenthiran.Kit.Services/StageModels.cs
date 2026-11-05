namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-2</c>: which model each pipeline stage runs on is a setting, not code, so a newer or
/// cheaper model is one change. Not used by anything yet.
/// </summary>
public static class StageModels
{
    /// <summary>Triage reads and sorts; the cheapest model does it (BEH-ORCH-1).</summary>
    public const string DefaultTriage = "claude-haiku-5-5";

    /// <summary>The stronger model that reviews a flagged stage from its bundle.</summary>
    public const string DefaultReview = "claude-opus-5-5";

    /// <summary>The spec and build stages do the work on a cheap model; only a review uses a stronger one (BEH-ORCH-3).</summary>
    public const string DefaultWork = "claude-haiku-5-5";

    /// <summary>
    /// The value of environment variable <paramref name="name"/>: unset or blank is <paramref name="fallback"/>;
    /// otherwise it must look like a model name, by the same rule a run's model is checked with. Anything else THROWS.
    /// </summary>
    public static string Parse(string name, string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback
        : KitRouter.ModelName.IsMatch(value.Trim()) ? value.Trim()
        : throw new InvalidOperationException($"{name} is \"{value}\": set it to a model name such as {fallback}");
}
