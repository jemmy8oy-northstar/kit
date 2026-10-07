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

        Js Bind(IReference? r)
        {
            if (r is null)
            {
                return Js.Undefined;
            }

            var key = $"{r.Kind}:{r.Name}";
            var b = Js.Prop(bindings, key);
            if (!b.Truthy && !missing.Contains(key, StringComparer.Ordinal))
            {
                missing.Add(key);
            }

            return b.Truthy ? b : Js.Undefined;
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

    private static List<string>? Emit(IStep step, Func<IReference?, Js> bind, JsonObject bindings, IReadOnlyDictionary<string, ISymbol> symbols)
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
    private static string? Locate(Js b)
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
    /// <c>Object.entries(bindings).find(([k, v]) =&gt; k.startsWith('file:') &amp;&amp; v.fixture)</c>
    /// — the first FILE binding with a fixture, in the object's own key order.
    /// </summary>
    private static Js? FirstFileFixture(JsonObject bindings)
    {
        foreach (var (key, value) in Js.Entries(bindings))
        {
            if (!key.StartsWith("file:", StringComparison.Ordinal))
            {
                continue;
            }

            // `null.fixture` throws in Node.
            var v = value ?? throw new InvalidOperationException($"binding {key} is null");
            var fixture = new Js(true, v).Get("fixture");
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
    private static string Stringify(string s) => Js.Quote(s);

    /// <summary>
    /// A JavaScript value read out of a bindings file: <see cref="Defined"/> false is
    /// <c>undefined</c>; a defined null <see cref="Node"/> is JSON <c>null</c>.
    /// </summary>
    private readonly record struct Js(bool Defined, JsonNode? Node)
    {
        public static readonly Js Undefined = new(false, null);

        public bool Truthy => Defined && Node switch
        {
            null => false,
            JsonValue v => v.GetValueKind() switch
            {
                JsonValueKind.String => v.GetValue<string>().Length > 0,
                JsonValueKind.Number => v.GetValue<double>() is var d && d != 0 && !double.IsNaN(d),
                JsonValueKind.True => true,
                _ => false,
            },
            _ => true,
        };

        /// <summary><c>obj[key]</c>: an own property of an object; anything else reads as undefined.</summary>
        public static Js Prop(JsonObject obj, string key) =>
            obj.TryGetPropertyValue(key, out var v) ? new Js(true, v) : Undefined;

        /// <summary>
        /// The own keys in the order <c>JSON.parse</c> leaves them: canonical array
        /// indices first, ascending, then every other key in source order.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, JsonNode?>> Entries(JsonObject obj) =>
            obj.Where(kv => IsIndex(kv.Key)).OrderBy(kv => uint.Parse(kv.Key, CultureInfo.InvariantCulture))
                .Concat(obj.Where(kv => !IsIndex(kv.Key)));

        /// <summary>
        /// <c>JSON.stringify</c>. Only ever called on a defined value or an
        /// <c>undefined</c> one, whose result interpolates as the text "undefined".
        /// </summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2).Append('"');
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // Other controls in lowercase hex; everything else raw —
                        // U+2028, `<`, `&` and `'` included, unlike System.Text.Json.
                        // ES2019 also escapes a LONE surrogate, which no input here
                        // can carry (see the test pinning the bindings-file case).
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.Append('"').ToString();
        }

        /// <summary><c>Number.prototype.toString()</c> for a finite double.</summary>
        public static string Number(double d)
        {
            if (d == 0)
            {
                return "0";
            }

            var sign = d < 0 ? "-" : string.Empty;

            // "R" is the shortest round-tripping form; only its LAYOUT differs from
            // JavaScript's, so take its digits and exponent and lay them out again.
            var r = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
            var parts = r.Split('E');
            var exp = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
            var mant = parts[0].Split('.');
            var digits = mant[0] + (mant.Length > 1 ? mant[1] : string.Empty);
            var n = mant[0].Length + exp;
            while (digits.Length > 1 && digits[0] == '0')
            {
                digits = digits[1..];
                n--;
            }

            digits = digits.TrimEnd('0');
            var k = digits.Length;

            if (k <= n && n <= 21)
            {
                return sign + digits + new string('0', n - k);
            }

            if (n > 0 && n <= 21)
            {
                return sign + digits[..n] + "." + digits[n..];
            }

            if (n > -6 && n <= 0)
            {
                return sign + "0." + new string('0', -n) + digits;
            }

            var e = n - 1;
            var es = (e >= 0 ? "+" : "-") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
            return sign + digits[0] + (k > 1 ? "." + digits[1..] : string.Empty) + "e" + es;
        }

        /// <summary><c>v[key]</c> on this value: only an object has own properties to read.</summary>
        public Js Get(string key) => Node is JsonObject o ? Prop(o, key) : Undefined;

        /// <summary><c>JSON.stringify(v)</c> as it interpolates into a template literal.</summary>
        public string Stringify() => Defined ? Json(Node) : "undefined";

        /// <summary><c>String(v)</c> — what <c>${v}</c> writes.</summary>
        public string AsString()
        {
            if (!Defined)
            {
                return "undefined";
            }

            return Node switch
            {
                null => "null",
                JsonObject => "[object Object]",
                JsonArray a => string.Join(",", a.Select(x => x is null ? string.Empty : new Js(true, x).AsString())),
                JsonValue v => v.GetValueKind() switch
                {
                    JsonValueKind.String => v.GetValue<string>(),
                    JsonValueKind.Number => Finite(v.GetValue<double>(), "Infinity"),
                    JsonValueKind.True => "true",
                    _ => "false",
                },
                _ => throw new InvalidOperationException("unreachable"),
            };
        }

        private static bool IsIndex(string key) =>
            uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var i)
            && i != uint.MaxValue && i.ToString(CultureInfo.InvariantCulture) == key;

        private static string Finite(double d, string infinity) =>
            double.IsInfinity(d) ? (d < 0 && infinity != "null" ? "-" : string.Empty) + infinity : Number(d);

        private static string Json(JsonNode? node) => node switch
        {
            null => "null",
            JsonObject o => "{" + string.Join(",", Entries(o).Select(kv => Quote(kv.Key) + ":" + Json(kv.Value))) + "}",
            JsonArray a => "[" + string.Join(",", a.Select(Json)) + "]",
            JsonValue v => v.GetValueKind() switch
            {
                JsonValueKind.String => Quote(v.GetValue<string>()),
                JsonValueKind.Number => Finite(v.GetValue<double>(), "null"),
                JsonValueKind.True => "true",
                _ => "false",
            },
            _ => throw new InvalidOperationException("unreachable"),
        };
    }
}
