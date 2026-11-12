namespace Balenthiran.Kit.Services;

/// <summary>
/// The model each stage runs on, as the <c>KIT_STAGE_MODEL_*</c> settings give them (BEH-ORCH-2, BEH-ORCH-3).
/// <paramref name="Context"/> is the context job's (BEH-ORCH-11); null is <see cref="StageModels.DefaultContext"/>.
/// </summary>
public sealed record StageModelSettings(string Triage, string Review, string Spec, string Build, string? ReviewLow = null, string? Context = null);
