using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The half every question shares, whoever raised it: what it cites, its options, a pick and the
/// strongest case against it, and the corpus line an answer becomes. A conflict and a behaviour
/// question carry it in two shapes; this reads either.
/// </summary>
internal sealed record Pack(string? Id, string? Asks, List<Option> Options, Recommendation? Recommend, string? Against, List<Citation> Cites)
{
    public static Pack Of(IQuestion q) => q switch
    {
        ConflictQuestion c => new Pack(null, c.Asks, c.Options, c.Recommend, c.Against, c.Cites),
        BehaviourQuestion b => new Pack(b.Id, b.Asks, b.Options, b.Recommend, b.Against, b.Cites),
        _ => throw new ArgumentException($"no question shape {q.GetType().Name}", nameof(q)),
    };

    public static void Render(List<string> l, IQuestion q)
    {
        var p = Of(q);

        // Before the options: the half of the evidence that makes the question concrete, and here
        // rather than in the review table because ticking it there would answer this silently.
        if (p.Cites.Count > 0)
        {
            l.Add("**Also on the table here** — these are part of this question, which is why they are not");
            l.Add("in the review list below:");
            l.Add(string.Empty);
            foreach (var c in p.Cites)
            {
                var contracts = string.Join("; ", c.Contracts);
                l.Add($"- `{c.Id}` {c.Title} — {(contracts.Length > 0 ? contracts : c.Title)} (`{QuestionSheet.Js(c.Ref)}`)");
            }

            l.Add(string.Empty);
        }

        l.Add("**Options**");
        l.Add(string.Empty);
        foreach (var o in p.Options)
        {
            l.Add($"- **{o.Label}** — {o.Consequence}");
        }

        l.Add(string.Empty);
        if (p.Recommend is not null)
        {
            l.Add($"**I'd pick: {p.Recommend.Label}.** {p.Recommend.Why}");
            l.Add(string.Empty);
            l.Add($"**The strongest case against that:** {QuestionSheet.Js(p.Against)}");
            l.Add(string.Empty);
        }

        l.Add("**Your answer** — becomes: " + AnswerLine(q, p));
        l.Add(string.Empty);
        l.Add("> ");
        l.Add(string.Empty);
    }

    // The one place that knows the mapping from "he said yes" to corpus text.
    private static string AnswerLine(IQuestion q, Pack p)
    {
        if (q is ConflictQuestion c)
        {
            // Names no winner: on the first real conflict the answer was neither side.
            return $"the two `provides` lines on {string.Join(" and ", c.Sides.Select(s => $"`{s.Id}`"))} reconciled — "
                + "corrected, merged into one statement, or one of them removed, whichever your answer implies.";
        }

        // kit#93: when the author wrote the options, answering IS picking one.
        if (p.Options.Count > 0)
        {
            return $"the option you pick, made true on `{p.Id}` — the edit that option describes above.";
        }

        if (q.Kind == "unserved")
        {
            return $"`serves BEH-…` added to `{p.Id}` (with the screen written into `DESIGN.md`), "
                + $"or `{p.Id}` deleted along with the surface it describes.";
        }

        return $"`review approved` on `{p.Id}`, or `review denied \"<what is actually true>\"`.";
    }
}
