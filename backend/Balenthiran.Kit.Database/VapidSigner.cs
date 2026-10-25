using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Balenthiran.Kit.Database;

/// <summary>
/// The VAPID token of RFC 8292: an ES256 JWT whose claims name the push service
/// (<c>aud</c>, the endpoint's origin), when it expires (<c>exp</c>) and who to contact
/// (<c>sub</c>). Signed with .NET's own ECDSA over P-256 from the key pair in <c>kit-push</c>
/// (base64url: the private scalar, and the uncompressed 65-byte public point). The signature is
/// JWS's fixed-width <c>r||s</c> (IEEE P1363), not DER.
/// </summary>
public sealed class VapidSigner(string subject, string publicKey, string privateKey)
{
    /// <summary>How long a token is good for; the RFC allows at most 24 hours.</summary>
    public static readonly TimeSpan Life = TimeSpan.FromHours(12);

    private const string Header = """{"typ":"JWT","alg":"ES256"}""";

    /// <summary>The token for the push service at <paramref name="audience"/> (scheme and authority, no path), valid from <paramref name="now"/>.</summary>
    public string Token(string audience, DateTimeOffset now)
    {
        var claims = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["exp"] = now.Add(Life).ToUnixTimeSeconds(),
            ["sub"] = subject,
        });
        var signing = Base64Url(Encoding.UTF8.GetBytes(Header)) + "." + Base64Url(Encoding.UTF8.GetBytes(claims));

        var point = FromBase64Url(publicKey);
        if (point.Length != 65 || point[0] != 0x04)
        {
            throw new FormatException("the VAPID public key is not an uncompressed P-256 point");
        }

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(privateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });
        var signature = key.SignData(Encoding.ASCII.GetBytes(signing), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return signing + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}
