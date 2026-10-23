namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// Early-access accounts (BEH-USERS-1): a name and a password hash for each user James created.
/// The operator, who signs in with <c>KIT_PASSWORD</c>, is not in it. Hashing is
/// <c>PasswordHash</c>'s; a store keeps the hash it is given and never sees a password.
/// </summary>
public interface IUserStore
{
    /// <summary>The stored hash for <paramref name="name"/>, or null when there is no such user.</summary>
    string? HashOf(string name);

    /// <summary>Record a new user; false, and nothing written, when the name is taken.</summary>
    bool Add(string name, string hash);

    /// <summary>Replace a user's hash; false when there is no such user.</summary>
    bool SetHash(string name, string hash);
}
