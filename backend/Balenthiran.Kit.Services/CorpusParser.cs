using System.Text.RegularExpressions;

using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Stage 1 of the engine: text in, behaviour tree out.
///
/// A direct port of <c>parse</c> and <c>parseStep</c> in
/// <c>prototypes/behaviour-ast/kit.js</c>, verified by reproducing the
/// <c>parse</c> section of every committed conformance golden byte-for-byte. The
/// Node engine is the specification; where this file and that one disagree, that
/// one is right.
///
/// Line-based on purpose: James's requirement is that a human can write the tree
/// by hand, and YAML and JSON both fail that on punctuation alone.
/// </summary>
public sealed class CorpusParser : ICorpusParser
{
    private static readonly HashSet<string> StepKeys = ["given", "when", "then", "contract"];

    // ── Whitespace, which is NOT `\s` ───────────────────────────────────────
    //
    // ⚠️ .NET's `\s` and JavaScript's `\s` are different sets, and the goldens
    // cannot tell them apart because no corpus contains a character in the
    // disagreement. .NET's includes U+0085 (NEXT LINE) and excludes U+FEFF
    // (BYTE ORDER MARK); JavaScript's is the reverse. A corpus saved by an editor
    // that writes a BOM would therefore parse differently under a `\s` port — the
    // first keyword would be `﻿behaviour` and every line would be outside a
    // behaviour.
    //
    // So the class is written out: ECMA-262's WhiteSpace plus LineTerminator,
    // which is also exactly what `String.prototype.trim` removes. Stated once as
    // regex source and once as characters, with a test asserting the two agree
    // member-for-member, so the pair cannot drift apart.
    private const string Ws =
        @"\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff";

    internal static readonly char[] WsChars =
    [
        '\t', '\n', '\v', '\f', '\r', ' ', '\u00a0', '\u1680',
        '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
        '\u2007', '\u2008', '\u2009', '\u200a',
        '\u2028', '\u2029', '\u202f', '\u205f', '\u3000', '\ufeff',
    ];

    // ⚠️ Every pattern is anchored with `\z`, never `$`. In .NET `$` ALSO matches
    // immediately before a trailing newline, so `$` would accept a line that
    // JavaScript's `$` rejects. Lines are trimmed before matching, which hides
    // the difference here today — `\z` means it cannot come back.
    private static readonly Regex BehaviourLine = new($@"^behaviour[{Ws}]+([A-Z][A-Z0-9-]*)[{Ws}]+""(.*)""\z", RegexOptions.Compiled);
    private static readonly Regex SourceRest = new($@"^(defined|inferred)(?:[{Ws}]+(.+))?\z", RegexOptions.Compiled);
    private static readonly Regex ReviewRest = new($@"^(unreviewed|approved|denied)(?:[{Ws}]+(.+))?\z", RegexOptions.Compiled);
    private static readonly Regex ServesRest = new(@"^([A-Z][A-Z0-9-]*)\z", RegexOptions.Compiled);
    private static readonly Regex Quoted = new(@"^""(.*)""\z", RegexOptions.Compiled);
    private static readonly Regex LabelAndText = new($@"^""([^""]*)""[{Ws}]+""(.*)""\z", RegexOptions.Compiled);
    private static readonly Regex CitesRest = new(@"^BEH-[A-Z0-9-]+\z", RegexOptions.Compiled);
    private static readonly Regex ProvidesRest = new($@"^([a-z]+):([A-Za-z0-9_]+)\.([a-zA-Z_]+)[{Ws}]*=[{Ws}]*(.+)\z", RegexOptions.Compiled);
    private static readonly Regex BareNoun = new(@"^[a-z]+:[A-Za-z0-9_]+\z", RegexOptions.Compiled);
    private static readonly Regex RefOrHole = new(@"\?([a-zA-Z_]+)|([a-z]+):([A-Za-z0-9_]+)|""([^""]*)""", RegexOptions.Compiled);

    /// <inheritdoc />
    /// <remarks>
    /// The list handed back is a <c>List&lt;Behaviour&gt;</c>, so its runtime
    /// type carries the concrete shape and key order the goldens are scored on —
    /// see <see cref="EngineJsonSerialiser.Serialise"/>.
    /// </remarks>
    public IReadOnlyList<IBehaviour> Parse(string text, string file = "<inline>")
    {
        var behaviours = new List<Behaviour>();
        Behaviour? cur = null;

        // `String.prototype.split('\n')` — a single-character split, not a
        // line-break-aware one. A lone `\r` is left on the end of the line and
        // removed by the trim below, which is why a CRLF corpus works.
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = Trim(lines[i]);
            var at = $"{file}:{i + 1}";
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var m = BehaviourLine.Match(line);
            if (m.Success)
            {
                cur = new Behaviour { Id = m.Groups[1].Value, Title = m.Groups[2].Value, At = at };
                behaviours.Add(cur);
                continue;
            }

            if (cur is null) throw new CorpusParseException($"{at}: line outside a behaviour: {line}");

            var kw = FirstToken(line);
            var rest = Trim(line[kw.Length..]);

            if (kw == "actor") { cur.Actor = rest; continue; }

            // ── James's #68 decision, 2026-08-30 ────────────────────────────
            // "I like this default included but marked unreviewed."
            //
            // The answer to a skipped adjudication step is not to block on
            // approval — it is to make the un-adjudicated count VISIBLE, so
            // skipping is a number someone can see rather than an absence nobody
            // can.
            if (kw == "source")
            {
                var s = SourceRest.Match(rest);
                if (!s.Success) throw new CorpusParseException($@"{at}: source wants ""defined""|""inferred"" [ref], got: {rest}");
                cur.Source = new Source { Origin = s.Groups[1].Value, Ref = Opt(s.Groups[2]) };

                // An inference is unreviewed until someone says otherwise.
                // Writing `source inferred` and having it default to approved
                // would reintroduce exactly the silence this removes.
                //
                // ⚠️ Guarded on `ReviewExplicit`, so a `review` line ABOVE a
                // `source inferred` line is not overwritten by it. The guard is
                // order-sensitive in the corpus, not in this file.
                if (s.Groups[1].Value == "inferred" && cur.ReviewExplicit is not true)
                {
                    cur.Review = new Review { State = "unreviewed", Note = null };
                }

                continue;
            }

            if (kw == "review")
            {
                var r = ReviewRest.Match(rest);
                if (!r.Success) throw new CorpusParseException($@"{at}: review wants ""unreviewed""|""approved""|""denied"" [note], got: {rest}");

                // A denial without a correction is a hole, not a decision — his
                // #68 point that a deny must say what correct behaviour looks
                // like. A bare denial deletes a line; a denial with a correction
                // compounds into the corpus.
                if (r.Groups[1].Value == "denied" && !r.Groups[2].Success)
                {
                    throw new CorpusParseException($"{at}: a denied behaviour must state the correction");
                }

                cur.Review = new Review { State = r.Groups[1].Value, Note = Opt(r.Groups[2]) };
                cur.ReviewExplicit = true;
                continue;
            }

            // ── James, kit#3 2026-08-30 ─────────────────────────────────────
            // "I feel like the api layer is inferred from what needs to be
            // displayed in the ui." So Kit does NOT grow a second assertion
            // layer for HTTP: an API behaviour exists to make some displayed
            // behaviour possible, and `serves` says which. The finding is the
            // ABSENCE — an inferred behaviour that serves nothing documented is
            // surface nothing displays.
            if (kw == "serves")
            {
                var s = ServesRest.Match(rest);
                if (!s.Success) throw new CorpusParseException($"{at}: serves wants a behaviour id, got: {rest}");
                cur.Serves.Add(new IdRef { Id = s.Groups[1].Value, At = at });
                continue;
            }

            // The split is: the TOOL finds where a real question exists, and a
            // HUMAN authors what the question actually asks. `asks` also
            // PROMOTES — mechanism sets the floor, not the ceiling.
            if (kw == "asks")
            {
                var a = Quoted.Match(rest);
                if (!a.Success) throw new CorpusParseException($"{at}: asks wants a quoted question, got: {rest}");
                cur.Asks = a.Groups[1].Value;
                continue;
            }

            // An option is a choice AND what changes if it is taken. A question
            // whose options all change nothing is a question that should never
            // have been asked, and writing the consequence down exposes it.
            if (kw == "option")
            {
                var o = LabelAndText.Match(rest);
                if (!o.Success) throw new CorpusParseException($@"{at}: option wants ""<label>"" ""<what changes if taken>"", got: {rest}");
                cur.Options.Add(new Option { Label = o.Groups[1].Value, Consequence = o.Groups[2].Value, At = at });
                continue;
            }

            if (kw == "recommend")
            {
                var r = LabelAndText.Match(rest);
                if (!r.Success) throw new CorpusParseException($@"{at}: recommend wants ""<option label>"" ""<why>"", got: {rest}");
                cur.Recommend = new Recommendation { Label = r.Groups[1].Value, Why = r.Groups[2].Value, At = at };
                continue;
            }

            // `cites` names a behaviour that is EVIDENCE for this question
            // rather than a question of its own, so it renders INSIDE the
            // decision and is suppressed from the review list. Deliberately not
            // automatic: two behaviours touching one symbol is not the same as
            // one being the other's evidence, and only an author knows which.
            if (kw == "cites")
            {
                if (!CitesRest.IsMatch(rest)) throw new CorpusParseException($"{at}: cites wants a behaviour id, got: {rest}");
                cur.Cites.Add(new IdRef { Id = rest, At = at });
                continue;
            }

            // A recommendation with no counter-case is advocacy wearing a
            // decision's clothes, and it is the half a reader most needs and the
            // author is least inclined to write — so this is required rather
            // than trusted.
            if (kw == "against")
            {
                var g = Quoted.Match(rest);
                if (!g.Success) throw new CorpusParseException($"{at}: against wants a quoted counter-case, got: {rest}");
                cur.Against = g.Groups[1].Value;
                continue;
            }

            if (kw == "provides")
            {
                // Where an inference is written down. It has to be an explicit,
                // reviewable line: an approve/deny needs something to point at
                // LATER, and an inference that lives only in a chat message
                // cannot be denied six weeks on.
                var p = ProvidesRest.Match(rest);
                if (!p.Success) throw new CorpusParseException($"{at}: provides wants <kind>:<Name>.<slot> = <value>, got: {rest}");
                cur.Provides.Add(new Provide
                {
                    Kind = p.Groups[1].Value,
                    Name = p.Groups[2].Value,
                    Slot = p.Groups[3].Value,
                    // `.split(',').map(trim).filter(Boolean)` — an empty entry is
                    // dropped, so `a,,b` is two values and a trailing comma adds
                    // none.
                    Value = p.Groups[4].Value.Split(',').Select(Trim).Where(v => v.Length > 0).ToList(),
                    From = cur.Id,
                    At = at,
                });
                continue;
            }

            if (StepKeys.Contains(kw))
            {
                var step = kw == "contract"
                    ? new Step { Kind = "contract", Verb = "contract", Text = rest, At = at }
                    : ParseStep(kw, rest, at);
                cur.Steps.Add(step);
                foreach (var h in step.Holes) cur.Unknowns.Add(new Unknown { Slot = h.Slot, At = at });
                continue;
            }

            throw new CorpusParseException($@"{at}: unrecognised keyword ""{kw}""");
        }

        return behaviours;
    }

    /// <summary>
    /// A step is a verb plus noun references. <c>?slot</c> marks a hole. A bare
    /// noun with no verb (<c>given transcription:Completed</c>) is a state
    /// precondition, and its verb is the literal string <c>state</c>.
    /// </summary>
    private static Step ParseStep(string kind, string rest, string at)
    {
        var first = FirstToken(rest);
        var verb = BareNoun.IsMatch(first) ? "state" : first;

        var refs = new List<Reference>();
        var holes = new List<Hole>();
        // ⚠️ `Match`, not `var`. `MatchCollection` implements both the generic and
        // the non-generic `IEnumerable`, so `var` binds the loop variable to
        // `object` and every `m.Groups[...]` below fails to compile. Nothing
        // caught this until the test project gained a reference to this one:
        // `dotnet test` on a test project that references nothing never builds
        // the library, and reports exit 0 with zero tests discovered.
        foreach (Match m in RefOrHole.Matches(rest))
        {
            if (m.Groups[1].Success) holes.Add(new Hole { Slot = m.Groups[1].Value });
            else if (m.Groups[2].Success) refs.Add(new Reference { Kind = m.Groups[2].Value, Name = m.Groups[3].Value });
            else refs.Add(new Reference { Kind = "literal", Name = m.Groups[4].Value });
        }

        return new Step { Kind = kind, Verb = verb, Text = rest, Refs = refs, Holes = holes, At = at };
    }

    /// <summary>
    /// <c>String.prototype.trim</c>, not <see cref="string.Trim()"/> — see the
    /// <c>Ws</c> comment for the two codepoints they disagree about.
    /// </summary>
    internal static string Trim(string s) => s.Trim(WsChars);

    /// <summary>
    /// <c>s.split(/\s+/)[0]</c> on an already-trimmed string: the leading run of
    /// non-whitespace. Empty in, empty out — a bare <c>given</c> with no text has
    /// the empty string as its verb, which is the Node engine's behaviour and not
    /// an oversight.
    /// </summary>
    private static string FirstToken(string s)
    {
        var i = s.IndexOfAny(WsChars);
        return i < 0 ? s : s[..i];
    }

    /// <summary>
    /// An unmatched optional group is <c>undefined</c> in JavaScript and becomes
    /// <c>null</c> via <c>|| null</c>. In .NET it is an unsuccessful
    /// <see cref="Group"/> whose <c>Value</c> is <c>""</c> — which would
    /// serialise as an empty string where the goldens have <c>null</c>.
    /// </summary>
    private static string? Opt(Group g) => g.Success ? g.Value : null;
}
