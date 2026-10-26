namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// BEH-USERS-4: what Kit remembers about one user, tied to no repo (memory about a repo lives in
/// that repo). One text body per user, keyed by the signed-in account's id and nothing a request carries.
/// </summary>
public interface IUserMemoryStore
{
    /// <summary>This user's memory; null when they have none yet.</summary>
    string? Read(string userId);

    /// <summary>Replace this user's memory.</summary>
    void Write(string userId, string body);
}
