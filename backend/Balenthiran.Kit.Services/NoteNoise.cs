namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-1</c>: the plain rule, with no model, that sets aside a note asking for nothing before any
/// run starts. Deliberately narrow: a note it does not catch goes to triage, which can still set it aside.
/// </summary>
public static class NoteNoise
{
    /// <summary>Why a stopped set-aside item made by the rule says it was made.</summary>
    public const string Reason = "a test note, no run started";

    private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
    {
        "test", "testing", "this is a test", "a test", "test note", "ignore",
    };

    /// <summary>True when <paramref name="text"/> is empty or only a test word, ignoring case, spacing and trailing punctuation.</summary>
    public static bool IsNoise(string? text)
    {
        var plain = (text ?? "").Trim().ToLowerInvariant().TrimEnd('.', '!', '?', ',', ';', ':', '-', ' ');
        return plain.Length == 0 || Words.Contains(plain);
    }
}
