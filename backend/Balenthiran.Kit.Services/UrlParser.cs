using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.DataModels.Models;

namespace Balenthiran.Kit.Services;

/// <summary>
/// The WHATWG URL Standard's basic parser, cut to what Kit reads (scheme, host, path,
/// origin) and to the one base it uses (<c>http://localhost/</c>). Query and fragment are
/// dropped, never parsed. Scored by <c>conformance/routes/host.json</c>.
///
/// ⚠️ Known, deliberate gaps, each in the direction that refuses rather than allows:
/// non-ASCII hosts use .NET's IDNA rather than UTS 46 exactly, and an IPv6 literal is
/// validated by <c>IPAddress</c>. A browser always sends an ASCII Origin, and Node's HTTP
/// parser refuses a non-ASCII target before <c>new URL</c> runs.
/// </summary>
public sealed class UrlParser : IUrlParser
{
    private static readonly Dictionary<string, int?> SpecialSchemes = new(StringComparer.Ordinal)
    {
        ["http"] = 80, ["https"] = 443, ["ws"] = 80, ["wss"] = 443, ["ftp"] = 21, ["file"] = null,
    };

    /// <inheritdoc />
    public IParsedUrl? Parse(string input, bool againstLocalhost)
    {
        var s = Clean(input);
        var colon = SchemeEnd(s);
        string scheme;
        string rest;
        if (colon > 0)
        {
            scheme = s[..colon].ToLowerInvariant();
            rest = s[(colon + 1)..];
        }
        else if (againstLocalhost)
        {
            scheme = "http";
            rest = s;
        }
        else
        {
            return null;
        }

        var special = SpecialSchemes.ContainsKey(scheme);
        rest = CutQueryAndFragment(rest);

        // The base's own scheme with no `//` is relative to the base; any other special
        // scheme skips every leading slash, either way round, to its authority.
        bool hasAuthority;
        if (special && againstLocalhost && scheme == "http")
        {
            hasAuthority = rest.Length >= 2 && IsSlash(rest[0], true) && IsSlash(rest[1], true);
            if (hasAuthority)
            {
                rest = rest[2..];
            }
        }
        else if (special)
        {
            hasAuthority = true;
            rest = rest.TrimStart('/', '\\');
        }
        else
        {
            hasAuthority = rest.StartsWith("//", StringComparison.Ordinal);
            if (hasAuthority)
            {
                rest = rest[2..];
            }
        }

        var host = againstLocalhost && !hasAuthority ? "localhost" : string.Empty;
        int? port = null;
        if (hasAuthority)
        {
            var end = 0;
            while (end < rest.Length && !IsSlash(rest[end], special))
            {
                end++;
            }

            if (ParseAuthority(rest[..end], special, scheme) is not { } authority)
            {
                return null;
            }

            (host, port) = authority;
            rest = rest[end..];
        }

        string pathname;
        if (!special && !hasAuthority && !rest.StartsWith('/'))
        {
            // An opaque path ("mailto:x"): kept as it is, C0 and non-ASCII encoded.
            var sb = new StringBuilder();
            for (var i = 0; i < rest.Length; i++)
            {
                AppendEncoded(sb, rest, ref i, PathSet.C0);
            }

            pathname = sb.ToString();
        }
        else
        {
            pathname = Path(rest, special);
        }

        string origin;
        if (special && SpecialSchemes[scheme] is { } defaultPort)
        {
            origin = $"{scheme}://{host}" + (port is { } p && p != defaultPort ? $":{p}" : string.Empty);
        }
        else
        {
            origin = "null";
        }

        return new ParsedUrl { Scheme = scheme, Host = host, Pathname = pathname, Origin = origin };
    }

    /// <summary>Leading and trailing C0 controls and spaces go; every tab and newline goes.</summary>
    private static string Clean(string input)
    {
        var t = input.Trim(Enumerable.Range(0, 0x21).Select(c => (char)c).ToArray());
        return t.Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>The index of the colon ending a scheme, or -1 when the input has none.</summary>
    private static int SchemeEnd(string s)
    {
        if (s.Length == 0 || !char.IsAsciiLetter(s[0]))
        {
            return -1;
        }

        for (var i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (c == ':')
            {
                return i;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return -1;
            }
        }

        return -1;
    }

    private static string CutQueryAndFragment(string s)
    {
        var q = s.IndexOfAny(['?', '#']);
        return q < 0 ? s : s[..q];
    }

    private static bool IsSlash(char c, bool special) => c == '/' || (special && c == '\\');

    /// <summary>Path state: dot segments (<c>.</c>, <c>%2e</c>, in any mix) resolved, then rejoined.</summary>
    private static string Path(string s, bool special)
    {
        var segments = new List<string>();
        var i = s.Length > 0 && IsSlash(s[0], special) ? 1 : 0;
        var buffer = new StringBuilder();
        for (; ; i++)
        {
            var end = i >= s.Length;
            if (end || IsSlash(s[i], special))
            {
                var segment = buffer.ToString();
                if (IsDoubleDot(segment))
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    if (end)
                    {
                        segments.Add(string.Empty);
                    }
                }
                else if (IsSingleDot(segment))
                {
                    if (end)
                    {
                        segments.Add(string.Empty);
                    }
                }
                else
                {
                    segments.Add(segment);
                }

                buffer.Clear();
                if (end)
                {
                    break;
                }
            }
            else
            {
                AppendEncoded(buffer, s, ref i, PathSet.Path);
            }
        }

        return "/" + string.Join('/', segments);
    }

    private static bool IsSingleDot(string s) => s == "." || s.Equals("%2e", StringComparison.OrdinalIgnoreCase);

    private static bool IsDoubleDot(string s) =>
        s == ".." || s.Equals(".%2e", StringComparison.OrdinalIgnoreCase) || s.Equals("%2e.", StringComparison.OrdinalIgnoreCase)
        || s.Equals("%2e%2e", StringComparison.OrdinalIgnoreCase);

    private enum PathSet
    {
        C0,
        Path,
    }

    /// <summary>Append <c>s[i]</c>, percent-encoded as UTF-8 if it is in the set; advances past a surrogate pair.</summary>
    private static void AppendEncoded(StringBuilder sb, string s, ref int i, PathSet set)
    {
        var c = s[i];
        var encode = c < 0x20 || c > 0x7E
            || (set == PathSet.Path && c is ' ' or '"' or '#' or '<' or '>' or '?' or '`' or '{' or '}');
        if (!encode)
        {
            sb.Append(c);
            return;
        }

        var length = char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;

        // A lone surrogate is U+FFFD, as the standard's UTF-8 encode makes it.
        var text = length == 1 && char.IsSurrogate(c) ? "�" : s.Substring(i, length);
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        i += length - 1;
    }

    /// <summary>Userinfo dropped, host parsed, port checked. Null wherever <c>new URL</c> throws.</summary>
    private static (string Host, int? Port)? ParseAuthority(string authority, bool special, string scheme)
    {
        var at = authority.LastIndexOf('@');
        var hostAndPort = at < 0 ? authority : authority[(at + 1)..];
        if (at >= 0 && hostAndPort.Length == 0)
        {
            return null;
        }

        // The host ends at the first colon outside brackets.
        var inBrackets = false;
        var colon = -1;
        for (var i = 0; i < hostAndPort.Length; i++)
        {
            if (hostAndPort[i] == '[')
            {
                inBrackets = true;
            }
            else if (hostAndPort[i] == ']')
            {
                inBrackets = false;
            }
            else if (hostAndPort[i] == ':' && !inBrackets)
            {
                colon = i;
                break;
            }
        }

        var rawHost = colon < 0 ? hostAndPort : hostAndPort[..colon];
        var rawPort = colon < 0 ? string.Empty : hostAndPort[(colon + 1)..];

        if (rawHost.Length == 0)
        {
            // A special URL must have a host; a port with no host is refused either way.
            return special || rawPort.Length > 0 ? null : (string.Empty, null);
        }

        int? port = null;
        if (rawPort.Length > 0)
        {
            if (!rawPort.All(char.IsAsciiDigit))
            {
                return null;
            }

            var value = BigInteger.Parse(rawPort, CultureInfo.InvariantCulture);
            if (value > 65535)
            {
                return null;
            }

            port = (int)value;
        }

        var host = special ? SpecialHost(rawHost) : OpaqueHost(rawHost);
        if (host is null)
        {
            return null;
        }

        // `file:` keeps no `localhost`; nothing Kit reads depends on it.
        return scheme == "file" && host == "localhost" ? (string.Empty, port) : (host, port);
    }

    private static string? SpecialHost(string input)
    {
        if (input.StartsWith('['))
        {
            return input.EndsWith(']') ? Ipv6(input[1..^1]) : null;
        }

        if (PercentDecode(input) is not { } decoded)
        {
            return null;
        }

        string ascii;
        if (decoded.All(char.IsAscii))
        {
            ascii = decoded.ToLowerInvariant();
        }
        else
        {
            try
            {
                ascii = new IdnMapping().GetAscii(decoded).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (ascii.Length == 0 || ascii.Any(IsForbiddenDomainCodePoint))
        {
            return null;
        }

        return EndsInANumber(ascii) ? Ipv4(ascii) : ascii;
    }

    private static string? OpaqueHost(string input)
    {
        if (input.StartsWith('['))
        {
            return input.EndsWith(']') ? Ipv6(input[1..^1]) : null;
        }

        return input.Any(c => c != '%' && IsForbiddenDomainCodePoint(c) && !(c < 0x20 || c == 0x7F)) ? null : input;
    }

    private static bool IsForbiddenDomainCodePoint(char c) =>
        c < 0x20 || c == 0x7F || c is ' ' or '#' or '%' or '/' or ':' or '<' or '>' or '?' or '@' or '[' or '\\' or ']' or '^' or '|';

    /// <summary>Percent-decoded bytes as UTF-8 (invalid sequences become U+FFFD, as the standard's decode does).</summary>
    private static string? PercentDecode(string s)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2]))
            {
                bytes.Add(Convert.ToByte(s.Substring(i + 1, 2), 16));
                i += 2;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(s[i].ToString()));
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool EndsInANumber(string host)
    {
        var parts = host.Split('.').ToList();
        if (parts[^1].Length == 0)
        {
            if (parts.Count == 1)
            {
                return false;
            }

            parts.RemoveAt(parts.Count - 1);
        }

        var last = parts[^1];
        if (last.Length > 0 && last.All(char.IsAsciiDigit))
        {
            return true;
        }

        return last.Length >= 2 && last[0] == '0' && (last[1] == 'x' || last[1] == 'X') && last[2..].All(char.IsAsciiHexDigit);
    }

    /// <summary>The IPv4 parser: up to four parts, each decimal, <c>0x</c> hex or leading-zero octal.</summary>
    private static string? Ipv4(string host)
    {
        var parts = host.Split('.').ToList();
        if (parts[^1].Length == 0 && parts.Count > 1)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count > 4)
        {
            return null;
        }

        var numbers = new List<BigInteger>();
        foreach (var part in parts)
        {
            if (Ipv4Number(part) is not { } n)
            {
                return null;
            }

            numbers.Add(n);
        }

        for (var i = 0; i < numbers.Count - 1; i++)
        {
            if (numbers[i] > 255)
            {
                return null;
            }
        }

        if (numbers[^1] >= BigInteger.Pow(256, 5 - numbers.Count))
        {
            return null;
        }

        var address = numbers[^1];
        for (var i = 0; i < numbers.Count - 1; i++)
        {
            address += numbers[i] * BigInteger.Pow(256, 3 - i);
        }

        var value = (uint)address;
        return $"{value >> 24}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
    }

    private static BigInteger? Ipv4Number(string part)
    {
        if (part.Length == 0)
        {
            return null;
        }

        var radix = 10;
        if (part.Length >= 2 && part[0] == '0' && (part[1] == 'x' || part[1] == 'X'))
        {
            part = part[2..];
            radix = 16;
        }
        else if (part.Length >= 2 && part[0] == '0')
        {
            part = part[1..];
            radix = 8;
        }

        BigInteger n = 0;
        foreach (var c in part)
        {
            var digit = char.IsAsciiDigit(c) ? c - '0' : char.IsAsciiHexDigit(c) ? char.ToLowerInvariant(c) - 'a' + 10 : 99;
            if (digit >= radix)
            {
                return null;
            }

            n = (n * radix) + digit;
        }

        return n;
    }

    private static string? Ipv6(string inner)
    {
        // A zone id (`%25…`) is refused by the standard and accepted by IPAddress.
        if (inner.Contains('%', StringComparison.Ordinal) || !IPAddress.TryParse(inner, out var address)
            || address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        return $"[{address}]";
    }
}
