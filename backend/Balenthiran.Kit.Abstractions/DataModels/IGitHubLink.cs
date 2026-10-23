namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>An installation of Kit's GitHub App that a user has shown is theirs (BEH-USERS-1).</summary>
public interface IGitHubLink
{
    /// <summary>The installation's id: what Kit later mints that user's repo tokens for.</summary>
    long Installation { get; }

    /// <summary>The GitHub account it is installed on, shown back to the user.</summary>
    string Account { get; }
}
