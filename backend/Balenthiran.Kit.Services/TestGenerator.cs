using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// Stage 3 of the engine, ported from <c>kit.js</c>'s <c>generate</c> and <c>emit</c>.
///
/// A binding says how a NAME in the spec becomes a thing on the page — one per
/// noun, not one per step. Each step either becomes lines of Playwright, or is
/// REFUSED: annotated for the runner and commented for the reader, never guessed.
/// A wire contract is commented and counted, never generated.
///
/// ⚠️ The output is JavaScript source assembled by string interpolation, so every
/// value a binding carries reaches it through a JavaScript coercion — <c>JSON.stringify</c>
/// for quoted values, <c>String(x)</c> for raw ones (<c>state</c>, <c>locator</c>),
/// truthiness for every test. Those three are reproduced below rather than
/// approximated with System.Text.Json, whose escaping and number formatting both
/// differ from Node's, because a binding value is free text a user wrote.
/// </summary>
public sealed class TestGenerator : ITestGenerator
{
    /// <summary>
    /// The annotation type a refused step carries into the Playwright report. Read
    /// by tooling; the tests spell the string out rather than reading this, so a
    /// rename cannot stay invisible by both sides agreeing with each other.
    /// </summary>
    public const string UngeneratedAnnotation = "kit-ungenerated";

    // `/:(\w+)/g`. JavaScript's `\w` is ASCII-only; .NET's is Unicode, so it is spelled out.
    private static readonly Regex RouteParam = new(":([A-Za-z0-9_]+)", RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public IGeneratedTest Generate(IBehaviour behaviour, JsonObject bindings, IReadOnlyDictionary<string, ISymbol> symbols)
    {
        var body = new List<string>();

        // A Set in `kit.js`: insertion order, no repeats.
        var missing = new List<string>();
        var stats = new GenerateStats();

        JsValue Bind(IReference? r)
        {
            if (r is null)
            {
                return JsValue.Undefined;
            }

            var key = $"{r.Kind}:{r.Name}";
            var b = JsValue.Prop(bindings, key);
            if (!b.Truthy && !missing.Contains(key, StringComparer.Ordinal))
            {
                missing.Add(key);
            }

            return b.Truthy ? b : JsValue.Undefined;
        }

        foreach (var step in behaviour.Steps)
        {
            if (step.Kind == "contract")
            {
                body.Add($"// CONTRACT (not derivable from a behaviour): {step.Text}");
                stats.Contract++;
                continue;
            }

            var lines = Emit(step, Bind, bindings, symbols);
            if (lines is not null)
            {
                body.AddRange(lines);
                stats.Generated += lines.Count;
            }
            else
            {
                // The annotation goes ABOVE the comment: the comment has to stay
                // directly above the action that depends on the refused step.
                body.Add($"test.info().annotations.push({{ type: {Stringify(UngeneratedAnnotation)}, description: {Stringify($"{step.Kind} {step.Text}")} }});");
                body.Add($"// UNGENERATED: {step.Kind} {step.Text}");
                stats.Ungenerated++;
            }
        }

        var code = new List<string> { $"test({Stringify($"[{behaviour.Id}] {behaviour.Title}")}, async ({{ page }}) => {{" };
        code.AddRange(body.Select(l => "  " + l));
        code.Add("});");

        return new GeneratedTest { Code = string.Join("\n", code), Missing = missing, Stats = stats };
    }

    private static List<string>? Emit(IStep step, Func<IReference?, JsValue> bind, JsonObject bindings, IReadOnlyDictionary<string, ISymbol> symbols)
    {
        var nouns = step.Refs.Where(r => r.Kind != "literal").ToList();
        var literal = step.Refs.FirstOrDefault(r => r.Kind == "literal");

        switch (step.Verb)
        {
            case "state":
            {
                var b = bind(nouns.FirstOrDefault());
                var state = b.Get("state");
                return b.Truthy && state.Truthy ? [$"await {state.AsString()};"] : null;
            }

            case "opens":
            {
                var noun = nouns.FirstOrDefault();
                var b = bind(noun);
                var route = b.Get("route");
                if (!b.Truthy || !route.Truthy)
                {
                    return null;
                }

                // `b.route.match(...)` — a route that is not a string throws in Node.
                var text = route.Node is JsonValue v && v.GetValueKind() == JsonValueKind.String
                    ? v.GetValue<string>()
                    : throw new InvalidOperationException($"binding {noun!.Kind}:{noun.Name} has a route that is not a string");

                // A parameterised route is a HOLE: refuse rather than strip it.
                foreach (var p in RouteParam.Matches(text).Select(m => m.Value).ToList())
                {
                    if (!symbols.TryGetValue($"{noun!.Kind}:{noun.Name}.{p[1..]}", out var sym))
                    {
                        return null;
                    }

                    text = ReplaceFirst(text, p, string.Concat(sym.Value));
                }

                return [$"await page.goto({Stringify(text)});"];
            }

            case "activates":
            {
                var l = Locate(bind(nouns.FirstOrDefault()));
                return l is not null ? [$"await page.{l}.click();"] : null;
            }

            case "sees":
            {
                var l = Locate(bind(nouns.FirstOrDefault()));
                return l is not null ? [$"await expect(page.{l}).toBeVisible();"] : null;
            }

            case "shows":
            {
                var l = Locate(bind(nouns.FirstOrDefault()));
                return l is not null && literal is not null
                    ? [$"await expect(page.{l}).toContainText({Stringify(literal.Name)});"]
                    : null;
            }

            case "attaches":
            {
                var file = bind(nouns.FirstOrDefault(n => n.Kind == "file"));
                var field = bind(nouns.FirstOrDefault(n => n.Kind == "field"));
                if (!file.Truthy || !field.Truthy)
                {
                    return null;
                }

                // A binding EXISTING is not the same as it carrying what this verb
                // needs: no fixture, or no locator, would emit a line that is counted
                // as generated and throws the moment it runs. Refuse instead.
                var l = Locate(field);
                var fixture = file.Get("fixture");
                if (l is null || !fixture.Truthy)
                {
                    return null;
                }

                return [$"await page.{l}.setInputFiles({fixture.Stringify()});"];
            }

            case "lands":
            {
                var b = bind(nouns.FirstOrDefault());
                var pattern = b.Get("urlPattern");
                return b.Truthy && pattern.Truthy
                    ? [$"await expect(page).toHaveURL(new RegExp({pattern.Stringify()}));"]
                    : null;
            }

            case "fills":
            {
                // `fills field:X with "text"` / `with ?slot` — one field, one value
                // (kit#151). Bind FIRST, so an unbound field is named even when the
                // value is what refuses.
                var one = nouns.FirstOrDefault(n => n.Kind == "field");
                if (one is not null)
                {
                    var fb = bind(one);
                    var label = fb.Get("label");
                    var value = literal is not null ? literal.Name : ProvidedValue(step);
                    return fb.Truthy && label.Truthy && value is not null
                        ? [$"await page.getByLabel({label.Stringify()}).fill({Stringify(value)});"]
                        : null;
                }

                // This step named no field: the fields come from what resolve wrote
                // onto it, filled by ANOTHER behaviour's `provides`.
                if (step.Resolved is null || !step.Resolved.TryGetValue("fields", out var fields))
                {
                    return null;
                }

                // Bind them ALL before refusing, so every missing field is named at
                // once rather than one per round.
                var bound = fields.Select(f => bind(new Reference { Kind = "field", Name = f })).ToList();
                if (bound.Any(fb => !fb.Truthy))
                {
                    return null;
                }

                var lines = new List<string>();
                foreach (var fb in bound)
                {
                    // This verb only ever emits getByLabel, so it needs a LABEL — not
                    // addressability in general.
                    var label = fb.Get("label");
                    if (!label.Truthy)
                    {
                        return null;
                    }

                    var fixture = FirstFileFixture(bindings);
                    lines.Add(fixture is { } fx
                        ? $"await page.getByLabel({label.Stringify()}).setInputFiles({fx.Stringify()});"
                        : $"await page.getByLabel({label.Stringify()}).fill('');");
                }

                return lines;
            }

            default:
                return null;
        }
    }

    /// <summary><c>loc(b)</c>: a role, else a label, else a raw locator, else nothing.</summary>
    private static string? Locate(JsValue b)
    {
        if (!b.Truthy)
        {
            return null;
        }

        var role = b.Get("role");
        if (role.Truthy)
        {
            var exact = b.Get("exact").Truthy ? ", exact: true" : string.Empty;
            return $"getByRole({role.Stringify()}, {{ name: {b.Get("name").Stringify()}{exact} }})";
        }

        var label = b.Get("label");
        if (label.Truthy)
        {
            return $"getByLabel({label.Stringify()})";
        }

        var locator = b.Get("locator");
        return locator.Truthy ? locator.AsString() : null;
    }

    /// <summary>
    /// <c>providedValue(step)</c>: what the step's first hole resolved to, or null. Exactly
    /// ONE value — <c>provides</c> splits on commas, and re-joining would guess the spacing.
    /// </summary>
    private static string? ProvidedValue(IStep step)
    {
        var hole = step.Holes.FirstOrDefault();
        return hole is not null && step.Resolved?.GetValueOrDefault(hole.Slot) is { Count: 1 } v ? v[0] : null;
    }

    /// <summary>
    /// <c>Object.entries(bindings).find(([k, v]) =&gt; k.startsWith('file:') &amp;&amp; v.fixture)</c>
    /// — the first FILE binding with a fixture, in the object's own key order.
    /// </summary>
    private static JsValue? FirstFileFixture(JsonObject bindings)
    {
        foreach (var (key, value) in JsValue.Entries(bindings))
        {
            if (!key.StartsWith("file:", StringComparison.Ordinal))
            {
                continue;
            }

            // `null.fixture` throws in Node.
            var v = value ?? throw new InvalidOperationException($"binding {key} is null");
            var fixture = new JsValue(true, v).Get("fixture");
            if (fixture.Truthy)
            {
                return fixture;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>String.prototype.replace</c> with a STRING pattern: the first occurrence
    /// only, and the replacement's <c>$</c> patterns are still expanded. A symbol
    /// value is free text, so <c>$&amp;</c> in one must do what it does in Node.
    /// </summary>
    private static string ReplaceFirst(string text, string pattern, string replacement)
    {
        var at = text.IndexOf(pattern, StringComparison.Ordinal);
        if (at < 0)
        {
            return text;
        }

        var sub = new StringBuilder();
        for (var i = 0; i < replacement.Length; i++)
        {
            var c = replacement[i];
            if (c != '$' || i + 1 >= replacement.Length)
            {
                sub.Append(c);
                continue;
            }

            switch (replacement[i + 1])
            {
                case '$': sub.Append('$'); i++; break;
                case '&': sub.Append(pattern); i++; break;
                case '`': sub.Append(text, 0, at); i++; break;
                case '\'': sub.Append(text, at + pattern.Length, text.Length - at - pattern.Length); i++; break;
                default: sub.Append(c); break;
            }
        }

        return string.Concat(text.AsSpan(0, at), sub.ToString(), text.AsSpan(at + pattern.Length));
    }

    /// <summary><c>JSON.stringify</c> of a string.</summary>
    private static string Stringify(string s) => JsValue.Quote(s);
}
