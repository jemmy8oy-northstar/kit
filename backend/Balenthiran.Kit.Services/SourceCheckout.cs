namespace Balenthiran.Kit.Services;

/// <summary>
/// Kit's own checkout (<c>KitSettings.RepoRoot</c>) and, when the entrypoint knows it, the <c>owner/name</c>
/// it is a clone of (BEH-HEALTH-4): the only source Kit can hash on its own disk.
/// </summary>
public sealed record SourceCheckout(string Root, string? Repo);
