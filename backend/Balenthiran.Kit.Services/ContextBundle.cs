using System.Text;
using System.Text.RegularExpressions;

namespace Balenthiran.Kit.Services;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-1</c>: what a triage run leaves on a work item's branch so the next stage starts
/// from it and need not search. A markdown file with a strict shape, so an orchestrator can reject bad stage
/// output instead of passing it on:
/// <code>
/// # Title
/// Work-item: the-slug
/// Flag: one-line reason        (optional, BEH-ORCH-2)
/// Cards:
/// - BEH-X-1                    (existing id, or "new: one line")
/// Notes:
/// - kit-ui.notes.md#2026-11-04-1807
/// Files:
/// - path/in/repo.cs
/// Context:
/// free text to the end of the file
/// </code>
/// </summary>
public sealed record ContextBundle(
    string Title,
    string Slug,
    IReadOnlyList<string> Cards,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Files,
    string Context,
    string? Flag = null)
{
    /// <summary>Lower-case words joined by single hyphens: safe in a branch name and a file name.</summary>
    public static readonly Regex SlugPattern = new(@"^[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.Compiled);

    private static readonly Regex CardId = new(@"^[A-Z][A-Z0-9]*(-[A-Z0-9]+)+\z", RegexOptions.Compiled);
    private static readonly Regex SectionLine = new(@"^[A-Za-z][A-Za-z-]*:\s*\z", RegexOptions.Compiled);

    /// <summary>The longest slug, so <c>kit/work/&lt;slug&gt;</c> stays a sane branch name.</summary>
    public const int MaxSlug = 60;

    private const string NewPrefix = "new: ";

    /// <summary>The bundle as markdown. THROWS for a bundle <see cref="Parse"/> would refuse.</summary>
    public string Render()
    {
        if (Check() is { } why)
        {
            throw new InvalidOperationException($"not a valid context bundle: {why}");
        }

        var sb = new StringBuilder();
        sb.Append("# ").Append(Title).Append('\n');
        sb.Append("Work-item: ").Append(Slug).Append('\n');
        if (Flag is not null)
        {
            sb.Append("Flag: ").Append(Flag).Append('\n');
        }

        Section(sb, "Cards", Cards);
        Section(sb, "Notes", Notes);
        Section(sb, "Files", Files);
        sb.Append("Context:\n").Append(Context.Trim()).Append('\n');
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string name, IReadOnlyList<string> items)
    {
        sb.Append(name).Append(":\n");
        foreach (var i in items)
        {
            sb.Append("- ").Append(i).Append('\n');
        }
    }

    /// <summary>Why this bundle could not be rendered or round-tripped, or null when it can.</summary>
    public string? Check()
    {
        if (OneLine(Title) is null) return "the title is empty or spans lines";
        if (Slug.Length > MaxSlug || !SlugPattern.IsMatch(Slug)) return $"the work-item slug \"{Slug}\" must be lower-case words joined by hyphens, at most {MaxSlug} characters";
        if (Flag is not null && OneLine(Flag) is null) return "the flag needs a one-line reason";
        if (Cards.Count == 0) return "Cards is empty: name an existing card id, or \"new: <one line>\"";
        foreach (var c in Cards)
        {
            if (!(c.StartsWith(NewPrefix, StringComparison.Ordinal) && OneLine(c[NewPrefix.Length..]) is not null) && !CardId.IsMatch(c))
            {
                return $"card \"{c}\" is neither an id such as BEH-ORCH-1 nor \"new: <one line>\"";
            }
        }

        foreach (var (name, items) in new[] { ("Notes", Notes), ("Files", Files) })
        {
            if (items.Any(i => OneLine(i) is null || i != i.Trim()))
            {
                return $"a {name} entry is empty, padded, or spans lines";
            }
        }

        return null;
    }

    private static string? OneLine(string s) =>
        string.IsNullOrWhiteSpace(s) || s.Contains('\n') || s.Contains('\r') ? null : s;

    /// <summary>The outcome of <see cref="Parse"/>: exactly one of the bundle and the reason it was refused.</summary>
    public sealed record Parsed(ContextBundle? Bundle, string? Error)
    {
        /// <summary>True when the text was a valid bundle.</summary>
        public bool Ok => Bundle is not null;
    }

    /// <summary>Reads a bundle back. Never throws on bad text: a malformed bundle is an <see cref="Parsed.Error"/> naming the line.</summary>
    public static Parsed Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Fail("the bundle is empty");
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var at = 0;

        if (!lines[at].StartsWith("# ", StringComparison.Ordinal)) return Fail("line 1 must be the title, \"# <title>\"");
        var title = lines[at++][2..].Trim();

        if (at >= lines.Length || !lines[at].StartsWith("Work-item:", StringComparison.Ordinal)) return Fail("line 2 must be \"Work-item: <slug>\"");
        var slug = lines[at++]["Work-item:".Length..].Trim();
        if (slug.Length == 0) return Fail("the work-item slug is missing");

        string? flag = null;
        if (at < lines.Length && lines[at].StartsWith("Flag:", StringComparison.Ordinal))
        {
            flag = lines[at++]["Flag:".Length..].Trim();
        }

        var lists = new Dictionary<string, List<string>>();
        foreach (var name in new[] { "Cards", "Notes", "Files" })
        {
            while (at < lines.Length && lines[at].Trim().Length == 0) at++;
            if (at >= lines.Length || lines[at].TrimEnd() != name + ":")
            {
                var got = at < lines.Length && SectionLine.IsMatch(lines[at]) ? $" (found \"{lines[at].TrimEnd()}\")" : "";
                return Fail($"expected the \"{name}:\" section at line {at + 1}{got}; sections are Cards, Notes, Files, Context, in that order");
            }

            at++;
            var items = new List<string>();
            for (; at < lines.Length; at++)
            {
                var line = lines[at];
                if (line.Trim().Length == 0) continue;
                if (line.StartsWith("- ", StringComparison.Ordinal)) { items.Add(line[2..].Trim()); continue; }
                if (SectionLine.IsMatch(line))
                {
                    break;
                }

                return Fail($"line {at + 1} is not a \"- item\" in {name}");
            }

            lists[name] = items;
        }

        if (at >= lines.Length || lines[at].TrimEnd() != "Context:")
        {
            var got = at < lines.Length ? $" (found \"{lines[at].TrimEnd()}\")" : "";
            return Fail($"expected the \"Context:\" section at line {at + 1}{got}; sections are Cards, Notes, Files, Context, in that order");
        }

        var context = string.Join('\n', lines[(at + 1)..]).Trim();
        var bundle = new ContextBundle(title, slug, lists["Cards"], lists["Notes"], lists["Files"], context, flag);
        return bundle.Check() is { } why ? Fail(why) : new Parsed(bundle, null);

        static Parsed Fail(string why) => new(null, why);
    }
}
