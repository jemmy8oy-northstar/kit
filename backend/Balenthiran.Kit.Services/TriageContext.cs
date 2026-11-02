using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// What the spec agent knows when it talks a card through (BEH-SPECAGENT-4): the card itself, as the
/// corpus writes it, and the project's outline — every epic, feature and behaviour by its title line.
/// The outline, not the whole corpus: kit-ui.beh is thousands of lines, and every turn re-sends this.
/// </summary>
public static class TriageContext
{
    /// <summary>The longest outline sent, in characters; past it the outline says it was cut.</summary>
    public const int MaxOutline = 40_000;

    /// <summary>The card and its project's outline, or null when <paramref name="id"/> is not a behaviour in <paramref name="corpus"/>.</summary>
    public static string? Build(string app, string id, string corpus)
    {
        var lines = corpus.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var head = $"behaviour {id}";
        var start = Array.FindIndex(lines, l => l == head || l.StartsWith(head + " ", StringComparison.Ordinal));
        if (start < 0)
        {
            return null;
        }

        // A block is its head line and the indented lines under it; the next unindented line starts another.
        var end = start + 1;
        while (end < lines.Length && (lines[end].Length == 0 || char.IsWhiteSpace(lines[end][0])))
        {
            end++;
        }

        var card = string.Join('\n', lines[start..end]).TrimEnd();
        var outline = new StringBuilder();
        foreach (var line in lines.Where(IsHead))
        {
            if (outline.Length + line.Length + 1 > MaxOutline)
            {
                outline.Append("… (the outline is cut here)\n");
                break;
            }

            outline.Append(line).Append('\n');
        }

        return $"Project: {app}\n\nThe card being decided:\n{card}\n\nThe project's spec, by title:\n{outline}";
    }

    private static bool IsHead(string line) =>
        line.StartsWith("epic ", StringComparison.Ordinal)
        || line.StartsWith("feature ", StringComparison.Ordinal)
        || line.StartsWith("behaviour ", StringComparison.Ordinal);
}
