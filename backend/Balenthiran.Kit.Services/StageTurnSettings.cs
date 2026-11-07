namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-5</c>: how many turns each pipeline stage's run may take. The limit travels with the run
/// (the claim's <c>maxTurns</c>), the way its model does. A run started by hand carries none and keeps the runner's own 100.
/// </summary>
public sealed record StageTurnSettings(int Triage, int Review, int Spec, int Build)
{
    public const int DefaultTriage = 15;
    public const int DefaultReview = 20;
    public const int DefaultSpec = 30;
    public const int DefaultBuild = 100;

    /// <summary>The limits when nothing is set.</summary>
    public static StageTurnSettings Defaults { get; } = new(DefaultTriage, DefaultReview, DefaultSpec, DefaultBuild);

    /// <summary>
    /// The value of environment variable <paramref name="name"/>: unset or blank is <paramref name="fallback"/>;
    /// otherwise a whole number above zero. Anything else THROWS.
    /// </summary>
    public static int Parse(string name, string? value, int fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback
        : int.TryParse(value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0 ? n
        : throw new InvalidOperationException($"{name} is \"{value}\": set it to a whole number of turns above zero, such as {fallback}");
}
