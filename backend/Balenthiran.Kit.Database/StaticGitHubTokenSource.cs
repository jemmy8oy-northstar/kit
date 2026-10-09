using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <c>KIT_GIT_TOKEN</c> as it is — the credential the image already pushes with (kit#144).
/// Trimmed: a secret created from a file ends in a newline, and .NET throws a FormatException
/// for one in a header value (kit#160's blind review).
/// </summary>
public sealed class StaticGitHubTokenSource(string? token) : IGitHubTokenSource
{
    /// <inheritdoc />
    public Task<string?> TokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(token) ? null : token.Trim());
}
