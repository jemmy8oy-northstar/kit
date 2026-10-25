using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Balenthiran.Kit.Database;

/// <summary>
/// RFC 8291 payload encryption (<c>Content-Encoding: aes128gcm</c>) for one Web Push message, with
/// the platform's own ECDH, HKDF and AES-GCM. The <c>WebPush</c> package (1.0.13) can only produce the
/// older <c>aesgcm</c> draft, which Apple's push service for home-screen web apps does not take, so
/// the package signs the VAPID token and this encrypts the body.
/// </summary>
public sealed class PushPayloadEncryptor
{
    private const int RecordSize = 4096;
    private const int KeyIdLength = 65;
    private const byte LastRecord = 0x02;

    /// <summary>
    /// The request body for a device whose key is <paramref name="p256dh"/> and whose authentication
    /// secret is <paramref name="auth"/> (both base64url, as the browser gives them). A fresh
    /// ephemeral key and salt every call, so the same message never encrypts the same twice.
    /// </summary>
    public byte[] Encrypt(string p256dh, string auth, byte[] plaintext)
    {
        var uaPublic = FromBase64Url(p256dh);
        var authSecret = FromBase64Url(auth);
        if (uaPublic.Length != KeyIdLength || uaPublic[0] != 0x04)
        {
            throw new FormatException("the device's p256dh is not an uncompressed P-256 point");
        }

        using var ua = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uaPublic[1..33], Y = uaPublic[33..65] },
        });
        using var server = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = server.ExportParameters(false).Q;
        var asPublic = new byte[] { 0x04 }.Concat(q.X!).Concat(q.Y!).ToArray();

        var shared = server.DeriveRawSecretAgreement(ua.PublicKey);
        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), uaPublic, asPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);

        var salt = RandomNumberGenerator.GetBytes(16);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // One record: the message, then the delimiter that says it is the last. Short enough that
        // it fits (the caller cuts a body to ~180 characters); a longer one is the caller's bug.
        var record = Concat(plaintext, [LastRecord]);
        if (record.Length > RecordSize - 16)
        {
            throw new ArgumentException("the payload does not fit one push record", nameof(plaintext));
        }

        var sealedRecord = new byte[record.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, tag.Length))
        {
            aes.Encrypt(nonce, record, sealedRecord, tag);
        }

        var header = new byte[16 + 4 + 1];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), RecordSize);
        header[20] = KeyIdLength;
        return Concat(header, asPublic, sealedRecord, tag);
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '='));
    }
}
