using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Balenthiran.Kit.Services;

/// <summary>
/// An early-access user's password, stored as <c>pbkdf2-sha256$iterations$salt$hash</c> (base64),
/// so the iteration count can rise later without breaking the hashes already stored.
/// </summary>
public static class PasswordHash
{
    /// <summary>OWASP's 2023 figure for PBKDF2-HMAC-SHA256.</summary>
    public const int Iterations = 600_000;

    public const int MinLength = 12;

    public const int MaxLength = 200;

    private const string Scheme = "pbkdf2-sha256";

    // Verified against when the user does not exist, so a missing name costs what a wrong password does.
    private static readonly Lazy<string> Decoy = new(() => Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16))));

    /// <summary>A fresh salt and hash for <paramref name="password"/>.</summary>
    public static string Hash(string password, int iterations = Iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Derive(password, salt, iterations);
        return $"{Scheme}${iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Does <paramref name="password"/> match <paramref name="stored"/>? A null or malformed hash is
    /// false, after the same work a real one costs.
    /// </summary>
    public static bool Verify(string password, string? stored)
    {
        var parsed = stored is null ? null : Parse(stored);

        // A missing user or an unreadable hash still costs one derivation, against the decoy.
        var match = Matches(password, parsed ?? Parse(Decoy.Value)!.Value);
        return parsed is not null && match;
    }

    /// <summary>Why <paramref name="password"/> cannot be a new password, or null when it can.</summary>
    public static string? Refuse(string? password) => password switch
    {
        null => "the password is missing",
        { Length: < MinLength } => $"a password needs at least {MinLength} characters",
        { Length: > MaxLength } => $"a password has at most {MaxLength} characters",
        _ => null,
    };

    private static bool Matches(string password, (int Iterations, byte[] Salt, byte[] Hash) parsed) =>
        CryptographicOperations.FixedTimeEquals(Derive(password, parsed.Salt, parsed.Iterations), parsed.Hash);

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);

    private static (int Iterations, byte[] Salt, byte[] Hash)? Parse(string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < 1)
        {
            return null;
        }

        try
        {
            var hash = Convert.FromBase64String(parts[3]);
            return hash.Length == 32 ? (iterations, Convert.FromBase64String(parts[2]), hash) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
