namespace Balenthiran.Kit.Services;

/// <summary>A bundle a triage run says it pushed (BEH-ORCH-3): its slug, and its text, or null when it was too large to send.</summary>
public sealed record ReportedBundle(string Slug, string? Text);
