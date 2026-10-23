using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary>An installation as GitHub or the user store read it back (BEH-USERS-1).</summary>
internal sealed record GitHubInstallation(long Installation, string Account) : IGitHubLink;
