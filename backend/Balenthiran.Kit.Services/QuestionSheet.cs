using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Port of <c>kit.js</c>'s <c>asked</c>, <c>questionErrors</c> and <c>renderSheet</c>. Scored against
/// Node's output for every committed corpus in <c>Fixtures/Check/report-goldens.json</c>.
/// </summary>
/// <remarks>
/// The sheet has a second reader who has never seen the repo and will confidently fill any gap
/// it is left, so every question carries its own evidence inline, the brief says what the
/// assistant is for, and every answer names the corpus line it becomes. It renders from the
/// questions rather than being written by hand, because a hand-written sheet stops matching
/// the corpus the day after, while still reading as current.
/// </remarks>
public sealed class QuestionSheet : IQuestionSheet
{
    /// <inheritdoc />
    public IReadOnlyList<IQuestion> Asked(IReadOnlyList<IBehaviour> behaviours, IReadOnlyList<IConflict> conflicts)
    {
        var parsed = behaviours.Cast<Behaviour>().ToList();
        var byId = ById(parsed);

        // A conflict's question is already on the sheet as that conflict's decision.
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in conflicts.Cast<Conflict>())
        {
            var owner = c.Holders.Concat(c.Challengers.Select(x => x.From))
                .Select(byId.GetValueOrDefault)
                .FirstOrDefault(b => b is not null && !string.IsNullOrEmpty(b.Asks));
            if (owner is not null)
            {
                owners.Add(owner.Id);
            }
        }

        var cited = parsed.SelectMany(b => b.Cites).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return parsed
            .Where(b => b.Source.Origin == "defined" && !string.IsNullOrEmpty(b.Asks) && !owners.Contains(b.Id) && !cited.Contains(b.Id))
            .Select(b => (IQuestion)new BehaviourQuestion
            {
                Kind = "asked",
                Tier = "asked",
                Key = b.Id,
                Id = b.Id,
                Title = b.Title,
                Source = b.Source,
                Serves = b.Serves.Select(s => s.Id).ToList(),
                Contracts = Contracts(b),
                Asks = b.Asks,
                Options = b.Options,
                Recommend = b.Recommend,
                Against = b.Against,
                Cites = b.Cites.Select(c =>
                {
                    var t = byId.GetValueOrDefault(c.Id);
                    return new Citation { Id = c.Id, Title = t?.Title ?? c.Id, Ref = t?.Source.Ref, Contracts = t is null ? [] : Contracts(t) };
                }).ToList(),
            })
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Errors(IEnumerable<IQuestion> questions)
    {
        var errors = new List<string>();
        foreach (var q in questions)
        {
            var pack = Pack.Of(q);
            var where = q is ConflictQuestion ? $"conflict {q.Key}" : pack.Id;
            if (q.Tier == "decision" && string.IsNullOrEmpty(pack.Asks))
            {
                errors.Add(q is ConflictQuestion conflict
                    ? $"{where}: neither side states the question — put an \"asks\" on one of {string.Join(" or ", conflict.Sides.Select(s => s.Id))}"
                    : $"{where}: nothing documented displays it, so both answers change something — it needs an \"asks\"");
                continue;
            }

            if (string.IsNullOrEmpty(pack.Asks))
            {
                // A pack hanging off no question is a stranded opinion.
                if (pack.Options.Count > 0 || pack.Recommend is not null || !string.IsNullOrEmpty(pack.Against))
                {
                    errors.Add($"{where}: has option/recommend/against but no \"asks\" to attach them to");
                }

                continue;
            }

            if (pack.Options.Count < 2)
            {
                errors.Add($"{where}: a question needs at least 2 options, has {pack.Options.Count}");
            }

            // The rot case: an option is relabelled and the recommendation points at nothing.
            if (pack.Recommend is not null && !pack.Options.Any(o => o.Label == pack.Recommend.Label))
            {
                errors.Add($"{where}: recommends \"{pack.Recommend.Label}\", which is not one of its options ({string.Join(", ", pack.Options.Select(o => o.Label))})");
            }

            // A cited behaviour is SUPPRESSED from the review list, so a `cites` naming nothing
            // deletes a question from the sheet and leaves no trace.
            foreach (var c in pack.Cites)
            {
                if (string.IsNullOrEmpty(c.Ref))
                {
                    errors.Add($"{where}: cites {c.Id}, which is not a behaviour in this corpus — that silently drops it from the sheet");
                }
            }

            if (pack.Recommend is not null && string.IsNullOrEmpty(pack.Against))
            {
                errors.Add($"{where}: recommends without stating the strongest case against — that is advocacy, not a decision pack");
            }
        }

        return errors;
    }

    /// <inheritdoc />
    public string Render(string app, IReadOnlyList<IQuestion> questions, IReadOnlyList<IQuestion> asked, string rev)
    {
        var decisions = questions.Where(q => q.Tier == "decision").ToList();
        var reviews = questions.Where(q => q.Tier == "review").Cast<BehaviourQuestion>().ToList();
        var l = new List<string>();

        l.Add($"# Behaviour question sheet — `{app}`");
        l.Add(string.Empty);
        l.Add($"**{decisions.Count} decisions · {reviews.Count} reviews"
            + (asked.Count > 0 ? $" · {asked.Count} asked by the author" : string.Empty)
            + $".** Generated by `kit sheet {app}{(rev.Length > 0 ? $" --rev {rev}" : string.Empty)}`"
            + (rev.Length > 0 ? $", over the app at `{rev}`" : string.Empty)
            + ". Do not hand-edit — re-run it.");
        l.Add(string.Empty);
        l.Add("## What this is");
        l.Add(string.Empty);
        l.Add("Kit read this app's `docs/DESIGN.md` and its backend test names and built one list of");
        l.Add("behaviours from both. Everything it read out of the **code** is marked unreviewed until a");
        l.Add("human rules on it, because an inference that quietly becomes a specification is the failure");
        l.Add("this whole thing exists to prevent.");
        l.Add(string.Empty);
        l.Add("The two sections below are not the same job and should not take the same effort:");
        l.Add(string.Empty);
        l.Add("- **Decisions** — Kit can prove both answers change something: either nothing documented");
        l.Add("  displays this surface, or two behaviours contradict each other about one value. Each one");
        l.Add("  carries the evidence, the options, my recommendation, and the strongest case against it.");
        l.Add("- **Reviews** — the code asserts this, a documented screen needs it, and it looks right.");
        l.Add("  One line each. If one is wrong, it becomes a decision.");
        l.Add(string.Empty);
        l.Add("## Brief for the assistant (paste this too)");
        l.Add(string.Empty);
        l.Add("> You are helping the product owner **pressure-test** these decisions, not make them. For each:");
        l.Add("> argue the case *against* the recommendation as strongly as you can; say which option you would");
        l.Add("> pick and why in one sentence; and name anything the evidence does not settle. Do not invent a");
        l.Add("> third option unless the two on offer genuinely miss the point — and if you do, say which");
        l.Add("> evidence made you. Do not agree because the recommendation sounds reasonable; it was written");
        l.Add("> by the same system that wrote the question.");
        l.Add(string.Empty);
        l.Add("## How an answer comes back");
        l.Add(string.Empty);
        l.Add("Each question names the corpus line it becomes. Write the answer under it in any form — the");
        l.Add("line is what I will make true in `behaviours/" + app + ".beh`, and re-running this sheet then");
        l.Add("drops the question. Nothing here is answered by silence.");
        l.Add(string.Empty);

        l.Add("---");
        l.Add(string.Empty);
        l.Add($"## Decisions — {decisions.Count}");
        l.Add(string.Empty);
        if (decisions.Count == 0)
        {
            l.Add("_None. Kit could not prove that any open question has two answers that differ._");
        }

        for (var i = 0; i < decisions.Count; i++)
        {
            var q = decisions[i];
            var n = i + 1;
            if (q is ConflictQuestion c)
            {
                // Strictly what was MEASURED: a symbol collision. What it means is the authored question.
                l.Add($"### D{n}. {Js(c.Asks)}");
                l.Add(string.Empty);
                l.Add($"**Why this is a decision:** two behaviours state different values for `{c.Key}`.");
                l.Add("Both sides were read out of a document rather than out of code, so this is the spec");
                l.Add("disagreeing with itself, not the code drifting from it. Kit detects the collision; what it");
                l.Add("means is the question above.");
                l.Add(string.Empty);
                l.Add("| behaviour | says `" + c.Key + "` is | source |");
                l.Add("|---|---|---|");
                foreach (var s in c.Sides)
                {
                    l.Add($"| `{s.Id}` {s.Title} | `{string.Join(',', s.Value)}` | `{(string.IsNullOrEmpty(s.Ref) ? "—" : s.Ref)}` |");
                }

                l.Add(string.Empty);
            }
            else
            {
                var b = (BehaviourQuestion)q;
                l.Add($"### D{n}. {Js(b.Asks)}");
                l.Add(string.Empty);
                l.Add($"**`{b.Id}` — {b.Title}**");
                l.Add(string.Empty);
                if (b.Kind == "unserved")
                {
                    // kit#93: only that nothing documented displays it was measured — not that the code has it.
                    l.Add("**Why this is a decision:** you said the API layer is inferred from what the UI needs to");
                    l.Add("display. Kit inferred this surface and **no documented screen displays it**, so either the");
                    l.Add("design is missing a screen or the surface should go. Those are opposite edits.");
                }
                else
                {
                    l.Add("**Why this is a decision:** it serves a documented screen, so Kit would have filed it as a");
                    l.Add("routine review — it is here because a human said it is not routine.");
                }

                l.Add(string.Empty);
                Evidence(l, b);
            }

            Pack.Render(l, q);
        }

        // kit#73: after the decisions, because Kit proved nothing about these — before the reviews,
        // because a human's open question is not cheap either.
        if (asked.Count > 0)
        {
            l.Add("---");
            l.Add(string.Empty);
            l.Add($"## Asked by the author — {asked.Count}");
            l.Add(string.Empty);
            l.Add("A human wrote each of these behaviours and left a question on it. Kit did not detect them");
            l.Add("and cannot rank them: they are here because the author asked, not because Kit proved that");
            l.Add("both answers change something.");
            l.Add(string.Empty);
            for (var i = 0; i < asked.Count; i++)
            {
                var q = (BehaviourQuestion)asked[i];
                l.Add($"### A{i + 1}. {Js(q.Asks)}");
                l.Add(string.Empty);
                l.Add($"**`{q.Id}` — {q.Title}**");
                l.Add(string.Empty);
                Evidence(l, q);
                Pack.Render(l, q);
            }
        }

        l.Add("---");
        l.Add(string.Empty);
        l.Add($"## Reviews — {reviews.Count}");
        l.Add(string.Empty);
        if (reviews.Count == 0)
        {
            l.Add("_None._");
        }
        else
        {
            l.Add("The code asserts each of these and a documented screen needs it. Tick, or say what is wrong —");
            l.Add("a \"wrong\" here promotes it to a decision on the next run.");
            l.Add(string.Empty);
            l.Add("| # | behaviour | what the code asserts | read out of |");
            l.Add("|---|---|---|---|");
            for (var i = 0; i < reviews.Count; i++)
            {
                var q = reviews[i];
                var what = q.Contracts.Count > 0 ? string.Join("; ", q.Contracts) : q.Title;
                l.Add($"| R{i + 1} | `{q.Id}` {q.Title} | {what} | `{Js(q.Source.Ref)}` |");
            }

            l.Add(string.Empty);
            l.Add("Each becomes `review approved` on that behaviour, or `review denied \"<what is actually true>\"`.");
            l.Add(string.Empty);
        }

        l.Add("---");
        l.Add(string.Empty);
        l.Add("## What Kit is NOT asking you here");
        l.Add(string.Empty);
        l.Add("Worth stating, because a sheet that omits its own scope reads as complete:");
        l.Add(string.Empty);
        l.Add("- **The behaviours this app has documented but not built.** Kit refuses to generate a test for a");
        l.Add("  screen that does not exist and names the missing noun instead. That list is a build backlog,");
        l.Add("  not a question — it goes to zero on its own as the frontend lands.");
        l.Add("- **Anything read out of a document you wrote.** A defined behaviour is not up for adjudication");
        l.Add("  here; you already ruled on it by writing it down. It only reappears if it collides with");
        l.Add("  another defined behaviour — which is exactly what D1 is.");
        if (asked.Count > 0)
        {
            l.Add("  The exception is a question its author wrote on it, which is the section above the reviews.");
        }

        return string.Join('\n', l) + "\n";
    }

    // A JavaScript template literal's `${x}`: null prints as the word.
    internal static string Js(string? s) => s ?? "null";

    private static void Evidence(List<string> l, BehaviourQuestion q)
    {
        l.Add($"**Evidence** — read out of `{Js(q.Source.Ref)}`:");
        l.Add(string.Empty);
        foreach (var c in q.Contracts)
        {
            l.Add($"- {c}");
        }

        if (q.Serves.Count > 0)
        {
            l.Add($"- serves: {string.Join(", ", q.Serves.Select(s => $"`{s}`"))}");
        }

        // kit#93: an empty block reads as "nothing to see" rather than "nothing was captured".
        if (q.Contracts.Count == 0 && q.Serves.Count == 0)
        {
            l.Add("- _none recorded: the behaviour carries no contract lines and serves no screen_");
        }

        l.Add(string.Empty);
    }

    private static List<string> Contracts(Behaviour b) => b.Steps.Where(s => s.Kind == "contract").Select(s => s.Text).ToList();

    private static Dictionary<string, Behaviour> ById(List<Behaviour> behaviours)
    {
        // `new Map(entries)`: a later duplicate id wins.
        var byId = new Dictionary<string, Behaviour>(StringComparer.Ordinal);
        foreach (var b in behaviours)
        {
            byId[b.Id] = b;
        }

        return byId;
    }
}
