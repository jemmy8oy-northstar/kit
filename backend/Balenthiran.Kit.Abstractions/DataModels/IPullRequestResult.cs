namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What asking for a pull request did (kit#147). Opening one and finding one already open
/// are both success, kept apart so a second press of Commit reads as "still waiting on you"
/// rather than as a new proposal.
/// </summary>
public interface IPullRequestResult
{
    /// <summary>A new pull request exists because of this call.</summary>
    bool Opened { get; }

    /// <summary>One was already open from the same branch into the same base, and this is it.</summary>
    bool AlreadyOpen { get; }

    int? Number { get; }

    /// <summary>The pull request's page on GitHub, for the reader to open on a phone.</summary>
    string? Url { get; }

    /// <summary>Set whenever there is no pull request to show — GitHub's own words where GitHub spoke. Never contains the token.</summary>
    string? Reason { get; }
}
