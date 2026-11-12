namespace Balenthiran.Kit.Services;

/// <summary>
/// The model each stage runs on, as the <c>KIT_STAGE_MODEL_*</c> settings give them (BEH-ORCH-2, BEH-ORCH-3).
/// <paramref name="Context"/> is the context job's (BEH-ORCH-11); null is <see cref="StageModels.DefaultContext"/>.
/// <paramref name="Resolve"/> is the merge job's conflict-resolution run (BEH-ORCH-11, slice 7b); null is <paramref name="Build"/>.
/// <paramref name="Test"/> is the run of a private project's test suite (BEH-ORCH-12); null is <paramref name="Triage"/>'s model, as it needs no code judgement.
/// </summary>
public sealed record StageModelSettings(string Triage, string Review, string Spec, string Build, string? ReviewLow = null, string? Context = null, string? Resolve = null, string? Test = null);
