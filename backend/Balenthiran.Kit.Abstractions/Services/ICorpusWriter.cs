using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The only thing in Kit that changes a corpus (<c>writer.js</c>). Every edit is a
/// surgical splice of lines, never a re-serialisation, so a corpus keeps its comments;
/// and every edit is re-parsed and refused if it would not parse or would change any
/// behaviour other than its target. Pure: text in, text (or a refusal) out.
/// </summary>
public interface ICorpusWriter
{
    /// <summary>Append one step line to an existing behaviour.</summary>
    IWriteResult AddStep(string text, string id, string line);

    /// <summary>Append a whole new behaviour at the end of the file.</summary>
    IWriteResult AddBehaviour(string text, string id, string title, string? actor = null, IReadOnlyList<string>? steps = null, string? source = null, string? reference = null);

    /// <summary>Set a behaviour's review state, replacing its review line or inserting one.</summary>
    IWriteResult SetReview(string text, string id, string state, string? note = null);

    /// <summary>BEH-ARCH-1 slice B1: <see cref="SetReview"/> on an architecture decision; only that block's review line changes.</summary>
    IWriteResult SetDecisionReview(string text, string id, string state, string? note = null);

    /// <summary>Add or remove a behaviour's bare <c>paused</c> line (BEH-BOARD-16); asking for the state it is already in writes nothing new.</summary>
    IWriteResult SetPaused(string text, string id, bool paused, string? until = null);

    /// <summary>Sets the behaviour's <c>priority</c> line to 1 (first) to 5, adding it when absent. Setting what is already set rewrites nothing.</summary>
    IWriteResult SetPriority(string text, string id, int priority);

    /// <summary>
    /// Adds or removes the file's <c># kit:promote self</c> line (BEH-SETTINGS-4). Asking for the
    /// state it is already in rewrites nothing. A file-level directive, so no behaviour changes.
    /// </summary>
    IWriteResult SetPromote(string text, bool on);

    /// <summary>Adds or removes the file's <c># kit:goal maintained</c> line (BEH-NARR-2); the state it is already in changes nothing.</summary>
    IWriteResult SetGoalMaintained(string text, bool on);

    /// <summary>
    /// Sets the file's <c># kit:idle</c> line to one of <c>on-change</c>, <c>health</c>, <c>ideas</c>,
    /// <c>idle</c> (BEH-SETTINGS-5), adding it when absent. Asking for what it already says rewrites nothing.
    /// </summary>
    IWriteResult SetIdle(string text, string mode);

    /// <summary>
    /// Sets the file's <c># kit:idle</c> line to the ordered idle work (BEH-SETTINGS-6): <c>health</c>
    /// and/or <c>ideas</c>, each at most once; empty means <c>none</c>. Asking for what it already says rewrites nothing.
    /// </summary>
    IWriteResult SetIdleOrder(string text, IReadOnlyList<string> order);

    /// <summary>
    /// Sets one feature's <c># kit:switch</c> line for <c>operator</c> or <c>testers</c> (BEH-FLAGS-1).
    /// The default is stored as no line. Asking for what the file already says rewrites nothing.
    /// </summary>
    IWriteResult SetSwitch(string text, string feature, string group, bool on);

    /// <summary>
    /// Splice one behaviour's block out of the file (BEH-ACT-3). Refused, writing nothing, when
    /// the id is absent or any OTHER behaviour or question still <c>serves</c> or <c>cites</c> it.
    /// </summary>
    IWriteResult RemoveBehaviour(string text, string id);

    /// <summary>
    /// Replace one behaviour's step, counted from 0 in file order (BEH-ACT-2). The line keeps
    /// its indentation; refused unless the result is still a step and nothing else in the
    /// behaviour changed.
    /// </summary>
    IWriteResult UpdateStep(string text, string id, int index, string line);

    /// <summary>Replace one behaviour's title in its header line, and nothing else (BEH-ACT-2).</summary>
    IWriteResult Retitle(string text, string id, string title);

    /// <summary>
    /// Add one binding to a bindings file's text. Refuses to overwrite one, or to change any
    /// other. <paramref name="corpora"/> is every corpus's noun names, for <c>sharedWith</c>.
    /// </summary>
    IWriteResult AddBinding(string text, string noun, JsonElement value, IReadOnlyDictionary<string, IReadOnlyList<string>> corpora, string app);

    /// <summary>
    /// Every parseable corpus's step-reference noun names (<c>corpusNouns</c>); one that
    /// will not parse is skipped and named in <paramref name="skipped"/>.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> CorpusNouns(IReadOnlyDictionary<string, string> corpora, List<string> skipped);
}
