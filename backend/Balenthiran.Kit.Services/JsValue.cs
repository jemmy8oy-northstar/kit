using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Services;

/// <summary>
/// A JavaScript value read out of a bindings file: <see cref="Defined"/> false is
/// <c>undefined</c>; a defined null <see cref="Node"/> is JSON <c>null</c>.
/// </summary>
internal readonly record struct JsValue(bool Defined, JsonNode? Node)
{
    public static readonly JsValue Undefined = new(false, null);

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
    public static JsValue Prop(JsonObject obj, string key) =>
        obj.TryGetPropertyValue(key, out var v) ? new JsValue(true, v) : Undefined;

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
                    var lone = (char.IsHighSurrogate(c) && !(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])))
                        || (char.IsLowSurrogate(c) && !(i > 0 && char.IsHighSurrogate(s[i - 1])));
                    if (c < 0x20 || lone)
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

    /// <summary>
    /// <c>JSON.parse</c>'s object model, built from a parsed element: a duplicate key
    /// keeps its FIRST position and its LAST value, as a JavaScript object does.
    /// </summary>
    public static JsonNode? FromElement(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var o = new JsonObject();
                foreach (var p in e.EnumerateObject())
                {
                    o[p.Name] = FromElement(p.Value);
                }

                return o;
            case JsonValueKind.Array:
                var a = new JsonArray();
                foreach (var x in e.EnumerateArray())
                {
                    a.Add(FromElement(x));
                }

                return a;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Number:
                // A JavaScript number is a double: `1.50` and `1.5` are one value.
                return JsonValue.Create(e.GetDouble());
            default:
                // Materialised, never `JsonValue.Create(e)`: that keeps a reference into
                // the document, which is disposed long before this node is written.
                return JsonValue.Create(StringOf(e));
        }
    }

    /// <summary>
    /// A JSON string's value. <c>GetString</c> refuses an escaped LONE surrogate, which
    /// <c>JSON.parse</c> accepts, so that case is unescaped by hand.
    /// </summary>
    private static string StringOf(JsonElement e)
    {
        try
        {
            return e.GetString()!;
        }
        catch (InvalidOperationException)
        {
            var raw = e.GetRawText();
            var sb = new StringBuilder();
            for (var i = 1; i < raw.Length - 1; i++)
            {
                if (raw[i] != '\\')
                {
                    sb.Append(raw[i]);
                    continue;
                }

                var c = raw[++i];
                sb.Append(c switch
                {
                    'b' => "\b",
                    'f' => "\f",
                    'n' => "\n",
                    'r' => "\r",
                    't' => "\t",
                    'u' => ((char)Convert.ToInt32(raw.Substring(i + 1, 4), 16)).ToString(),
                    _ => c.ToString(),
                });
                if (c == 'u')
                {
                    i += 4;
                }
            }

            return sb.ToString();
        }
    }

    /// <summary><c>JSON.stringify(v, null, 2)</c>, at a starting indent.</summary>
    public static string Indented(JsonNode? node, string indent = "")
    {
        var inner = indent + "  ";
        switch (node)
        {
            case JsonObject o:
                var props = Entries(o).ToList();
                return props.Count == 0
                    ? "{}"
                    : "{\n" + string.Join(",\n", props.Select(kv => inner + Quote(kv.Key) + ": " + Indented(kv.Value, inner))) + "\n" + indent + "}";
            case JsonArray a:
                return a.Count == 0
                    ? "[]"
                    : "[\n" + string.Join(",\n", a.Select(x => inner + Indented(x, inner))) + "\n" + indent + "]";
            default:
                return Json(node);
        }
    }

    /// <summary>Compact <c>JSON.stringify(v)</c> of a defined value.</summary>
    public static string Compact(JsonNode? node) => Json(node);

    /// <summary><c>v[key]</c> on this value: only an object has own properties to read.</summary>
    public JsValue Get(string key) => Node is JsonObject o ? Prop(o, key) : Undefined;

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
            JsonArray a => string.Join(",", a.Select(x => x is null ? string.Empty : new JsValue(true, x).AsString())),
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
