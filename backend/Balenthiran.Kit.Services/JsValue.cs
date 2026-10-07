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
