using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Ported from <c>writer.js</c>; scored by <c>conformance/routes/writes.json</c>. Three
/// rules, each one a reason this is a splice and not a re-serialisation:
/// (1) edit an array of LINES, so the corpus keeps the comments <c>Parse</c> drops;
/// (2) re-parse the result and refuse what will not parse;
/// (3) refuse any edit that changes the meaning of a behaviour other than the target.
/// </summary>
public sealed class CorpusWriter(ICorpusParser parser) : ICorpusWriter
{
    private const string Indent = "  ";

    private static readonly string Ws = CorpusParser.Ws;

    // `/^behaviour\s+([A-Z][A-Z0-9-]*)\s+"/` — JavaScript's `\s`, which is not .NET's.
    private static readonly Regex Header = new($@"\Gbehaviour[{Ws}]+([A-Z][A-Z0-9-]*)[{Ws}]+""", RegexOptions.Compiled);
    private static readonly Regex BehaviourId = new(@"^[A-Z][A-Z0-9-]*\z", RegexOptions.Compiled);

    /// <inheritdoc />
    public IWriteResult AddStep(string text, string id, string line)
    {
        var step = Trim(line);
        if (step.Length == 0)
        {
            return WriteResult.Refuse("empty-step", "a step line cannot be blank");
        }

        // A step that is two lines would splice two in while every range here assumes one.
        if (line.Contains('\n', StringComparison.Ordinal))
        {
            return WriteResult.Refuse("multiline-step", "a step is one line; add them one at a time");
        }

        if (Block(text, id) is not { } b)
        {
            return WriteResult.Refuse("no-such-behaviour", $"no behaviour {id} in this corpus", Ids(text));
        }

        var lines = text.Split('\n').ToList();
        lines.Insert(b.End + 1, Indent + step);
        return Validate(text, string.Join('\n', lines), id);
    }

    /// <inheritdoc />
    public IWriteResult AddBehaviour(string text, string id, string title, string? actor = null, IReadOnlyList<string>? steps = null, string? source = null, string? reference = null)
    {
        if (!BehaviourId.IsMatch(id))
        {
            return WriteResult.Refuse("bad-id", $"a behaviour id is uppercase letters, digits and hyphens, got: {id}");
        }

        if (Ids(text).Contains(id, StringComparer.Ordinal))
        {
            return WriteResult.Refuse("duplicate-id", $"{id} is already in this corpus");
        }

        // The header grammar is `behaviour ID "title"`: a quote would end the title early.
        if (title.Contains('"', StringComparison.Ordinal) || title.Contains('\n', StringComparison.Ordinal))
        {
            return WriteResult.Refuse("bad-title", "a title cannot contain a double quote or a newline");
        }

        // 🔴 SetReview's note guard, on the route that creates: rule 3 exempts the TARGET, and
        // here that is the new block, so `x\n  review approved` would arrive pre-approved.
        if ((steps ?? []).Any(s => s.Contains('\n', StringComparison.Ordinal)))
        {
            return WriteResult.Refuse("multiline-step", "a step is one line; add them one at a time");
        }

        foreach (var (field, value) in new[] { ("actor", actor), ("source", source), ("ref", reference) })
        {
            if (value?.Contains('\n', StringComparison.Ordinal) ?? false)
            {
                return WriteResult.Refuse("multiline-field", $"a behaviour's {field} is one line; it cannot contain a newline");
            }
        }

        var output = new List<string> { $"behaviour {id} \"{title}\"" };
        if (!string.IsNullOrEmpty(actor))
        {
            output.Add($"{Indent}actor {actor}");
        }

        // `inferred` unless told otherwise: a UI is a machine, and silence means a human wrote it.
        output.Add($"{Indent}source {(string.IsNullOrEmpty(source) ? "inferred" : source)}{(string.IsNullOrEmpty(reference) ? string.Empty : " " + reference)}");
        foreach (var s in steps ?? [])
        {
            output.Add(Indent + Trim(s));
        }

        // Exactly one blank line before the new block, and the trailing newline kept,
        // whatever the file ended with — or every edit alternates two spellings.
        var after = $"{text.TrimEnd(CorpusParser.WsChars)}\n\n{string.Join('\n', output)}\n";
        return Validate(text, after, id);
    }

    /// <inheritdoc />
    public IWriteResult SetReview(string text, string id, string state, string? note = null)
    {
        var st = Trim(state);
        var nt = note is null ? string.Empty : Trim(note);
        if (st.Length == 0)
        {
            return WriteResult.Refuse("empty-review", "a review needs a state: unreviewed, approved or denied");
        }

        // 🔴 A security guard, not tidiness: rule 3 exempts the TARGET, so a newline in a
        // note is the one way a caller's text could splice an extra line into it unpoliced.
        if (state.Contains('\n', StringComparison.Ordinal) || (note?.Contains('\n', StringComparison.Ordinal) ?? false))
        {
            return WriteResult.Refuse("multiline-review", "a review is one line; a state or a note cannot contain a newline");
        }

        if (Block(text, id) is not { } b)
        {
            return WriteResult.Refuse("no-such-behaviour", $"no behaviour {id} in this corpus", Ids(text));
        }

        var lines = text.Split('\n').ToList();
        var line = Indent + (nt.Length > 0 ? $"review {st} {nt}" : $"review {st}");

        var at = -1;
        for (var i = b.Start + 1; i <= b.End; i++)
        {
            if (FirstToken(lines[i]) == "review")
            {
                at = i;
                break;
            }
        }

        if (at != -1)
        {
            lines[at] = line;
        }
        else
        {
            // Under `source`; failing that, `actor`; failing that, the header.
            var anchor = b.Start;
            for (var i = b.Start + 1; i <= b.End; i++)
            {
                var kw = FirstToken(lines[i]);
                if (kw == "actor")
                {
                    anchor = i;
                }

                if (kw == "source")
                {
                    anchor = i;
                    break;
                }
            }

            lines.Insert(anchor + 1, line);
        }

        return Validate(text, string.Join('\n', lines), id);
    }

    /// <inheritdoc />
    public IWriteResult AddBinding(string text, string noun, JsonElement value, IReadOnlyDictionary<string, IReadOnlyList<string>> corpora, string app)
    {
        JsonNode? parsed;
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = int.MaxValue / 2 });
            parsed = JsValue.FromElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return WriteResult.Refuse("bindings-already-invalid", "bindings.json did not parse before this edit: it is not valid JSON");
        }

        if (parsed is not JsonObject bindings)
        {
            return WriteResult.Refuse("bindings-already-invalid", "bindings.json is not a JSON object");
        }

        var key = Trim(noun);
        if (!IsNoun(key))
        {
            return WriteResult.Refuse("bad-noun", $"a noun is <kind>:<Name>, lowercase kind and a capitalised name, got: {noun}");
        }

        // Rebinding changes what every behaviour mentioning the noun generates — not a click.
        if (bindings.TryGetPropertyValue(key, out var existing))
        {
            return new WriteResult
            {
                Ok = false,
                Error = "already-bound",
                Reason = $"{key} is already bound to {JsValue.Compact(existing)} — rebinding changes every corpus that mentions it, so it is not a click",
                Current = existing?.DeepClone(),
            };
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return WriteResult.Refuse("bad-binding", "a binding is a JSON object, e.g. {\"role\":\"button\",\"name\":\"Add habit\"}");
        }

        if (!value.EnumerateObject().Any())
        {
            return WriteResult.Refuse("bad-binding", "an empty binding binds nothing — it would satisfy no verb and still count as bound");
        }

        // writer.js's round-trip and collateral checks guard against values JSON cannot
        // carry (undefined, functions). A value that arrived AS JSON always survives, so
        // here they could never fire, and are not restated as code nothing can reach.
        var after = (JsonObject)bindings.DeepClone();
        after[key] = JsValue.FromElement(value);

        return new WriteResult
        {
            Ok = true,
            Text = JsValue.Indented(after) + "\n",
            Noun = key,
            SharedWith = corpora.Where(c => c.Key != app && c.Value.Contains(key, StringComparer.Ordinal)).Select(c => c.Key).OrderBy(a => a, StringComparer.Ordinal).ToList(),
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> CorpusNouns(IReadOnlyDictionary<string, string> corpora, List<string> skipped)
    {
        var output = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (app, text) in corpora)
        {
            try
            {
                var names = parser.Parse(text, app + ".beh")
                    .SelectMany(b => b.Steps)
                    .SelectMany(s => s.Refs)
                    .Where(r => r.Kind != "literal")
                    .Select(r => $"{r.Kind}:{r.Name}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList();
                output[app] = names;
            }
            catch (CorpusParseException)
            {
                // Skipped, not fatal — but REPORTED, or "could not look" reads as "nothing collides".
                skipped.Add(app);
            }
        }

        return output;
    }

    /// <summary>
    /// Is this string exactly one noun, by the PARSER's definition? Asked of the parser's
    /// own step grammar, and then required to spell the whole input back.
    /// </summary>
    private static bool IsNoun(string s)
    {
        var t = Trim(s);
        if (t.Length == 0)
        {
            return false;
        }

        var step = CorpusParser.ParseStep("noun-check", t, "noun-check");
        if (step.Holes.Count != 0 || step.Refs.Count != 1)
        {
            return false;
        }

        var r = step.Refs[0];
        return r.Kind != "literal" && $"{r.Kind}:{r.Name}" == t;
    }

    /// <summary>The one gate every edit goes through. Never throws, never writes.</summary>
    private WriteResult Validate(string before, string after, string id)
    {
        IReadOnlyList<IBehaviour> oldAst;
        IReadOnlyList<IBehaviour> newAst;
        try
        {
            oldAst = parser.Parse(before, "before");
        }
        catch (CorpusParseException e)
        {
            // Already broken: say so, rather than blaming the edit for someone else's syntax.
            return WriteResult.Refuse("corpus-already-invalid", $"the file did not parse before this edit: {e.Message}");
        }

        try
        {
            newAst = parser.Parse(after, "after");
        }
        catch (CorpusParseException e)
        {
            return WriteResult.Refuse("would-not-parse", e.Message);
        }

        // `new Map(ast.map(b => [b.id, b]))`: first position, last value.
        var (oldIds, oldById) = ById(oldAst);
        var (newIds, newById) = ById(newAst);
        foreach (var bid in oldIds)
        {
            if (bid == id)
            {
                continue;
            }

            if (!newById.TryGetValue(bid, out var n))
            {
                return WriteResult.Refuse("collateral-change", $"{bid} disappeared from the corpus");
            }

            if (Shape(n) != Shape(oldById[bid]))
            {
                return WriteResult.Refuse("collateral-change", $"{bid} changed, and only {id} was meant to");
            }
        }

        foreach (var bid in newIds)
        {
            if (bid != id && !oldById.ContainsKey(bid))
            {
                return WriteResult.Refuse("collateral-change", $"{bid} appeared, and only {id} was meant to");
            }
        }

        return new WriteResult { Ok = true, Text = after };
    }

    private static (List<string> Order, Dictionary<string, IBehaviour> ById) ById(IReadOnlyList<IBehaviour> ast)
    {
        var order = new List<string>();
        var byId = new Dictionary<string, IBehaviour>(StringComparer.Ordinal);
        foreach (var b in ast)
        {
            if (!byId.ContainsKey(b.Id))
            {
                order.Add(b.Id);
            }

            byId[b.Id] = b;
        }

        return (order, byId);
    }

    /// <summary>
    /// A comparable projection of one behaviour's MEANING, from the AST rather than the
    /// lines — a reformatted line that parses identically is not a change.
    /// </summary>
    private static string Shape(IBehaviour b) => JsonSerializer.Serialize(new
    {
        id = b.Id,
        title = b.Title,
        actor = b.Actor,
        steps = b.Steps.Select(s => $"{s.Kind}|{s.Verb}||{s.Text}").ToList(),
        provides = b.Provides.Select(p => $"{p.Kind}:{p.Name}.{p.Slot}={string.Join(',', p.Value)}").ToList(),
        serves = b.Serves.Select(s => s.Id).ToList(),
        source = new { origin = b.Source.Origin, @ref = b.Source.Ref },
        review = new { state = b.Review.State, note = b.Review.Note },
        asks = b.Asks,
        options = b.Options.Select(o => $"{o.Label}|{o.Consequence}").ToList(),
        recommend = b.Recommend is { } r ? $"{r.Label}|{r.Why}" : null,
        against = b.Against,
        cites = b.Cites.Select(c => c.Id).ToList(),
    });

    /// <summary>
    /// One behaviour's line range. <c>End</c> is its LAST CONTENT line, so blanks and
    /// comments trailing it — which introduce the NEXT block — stay outside.
    /// </summary>
    private static (int Start, int End)? Block(string text, string id)
    {
        var lines = text.Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Header.Match(lines[i]) is { Success: true } m && m.Groups[1].Value == id)
            {
                start = i;
                break;
            }
        }

        if (start == -1)
        {
            return null;
        }

        var end = start;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (Header.IsMatch(lines[i]))
            {
                break;
            }

            var t = Trim(lines[i]);
            if (t.Length > 0 && !t.StartsWith('#'))
            {
                end = i;
            }
        }

        return (start, end);
    }

    /// <summary>
    /// Every behaviour id, in order. JavaScript's <c>/^…/gm</c> starts a line after
    /// <c>\n</c>, <c>\r</c>, U+2028 and U+2029 — not after <c>\n</c> alone, as .NET's does.
    /// </summary>
    private static List<string> Ids(string text)
    {
        var ids = new List<string>();
        for (var p = 0; p < text.Length; p++)
        {
            if (p > 0 && text[p - 1] is not ('\n' or '\r' or '\u2028' or '\u2029'))
            {
                continue;
            }

            if (Header.Match(text, p) is { Success: true } m)
            {
                ids.Add(m.Groups[1].Value);
            }
        }

        return ids;
    }

    /// <summary><c>line.trim().split(/\s+/)[0]</c>.</summary>
    private static string FirstToken(string line)
    {
        var t = Trim(line);
        var i = t.IndexOfAny(CorpusParser.WsChars);
        return i < 0 ? t : t[..i];
    }

    private static string Trim(string s) => s.Trim(CorpusParser.WsChars);
}
