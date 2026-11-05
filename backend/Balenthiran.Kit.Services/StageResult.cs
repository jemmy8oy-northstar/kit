namespace Balenthiran.Kit.Services;

/// <summary>What a finished run reports: which stage it ran, whether it worked, the bundle it left, and the error if it failed.</summary>
public sealed record StageResult(PipelineStage Stage, bool Succeeded, ContextBundle? Bundle, string? Error);
