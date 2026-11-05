namespace Balenthiran.Kit.Services;

/// <summary>A triage run's task text, and how many of the notes given (from the first) it carries (kit-ui.beh <c>BEH-ORCH-1</c>).</summary>
public sealed record TriageTask(string Text, int NotesTaken);
