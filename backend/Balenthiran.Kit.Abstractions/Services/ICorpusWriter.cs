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
