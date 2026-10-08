using System.Text.Json.Nodes;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// The corpora on disk: <c>&lt;app&gt;.beh</c> beside an optional <c>&lt;app&gt;.bindings.json</c>.
/// The ONLY source of valid app names — a name is looked up in <see cref="Corpora"/>,
/// never joined to a path, so there is nothing to traverse with.
/// </summary>
public interface ICorpusDirectory
{
    /// <summary>Every app name, in code-unit order (<c>ui.js</c>'s <c>corpora()</c>).</summary>
    IReadOnlyList<string> Corpora();

    /// <summary>The corpus text of an app <see cref="Corpora"/> listed.</summary>
    string Read(string app);

    /// <summary>The app's bindings verbatim; no file binds nothing, which is a real state, not an error.</summary>
    JsonObject Bindings(string app);

    /// <summary>Where the corpus is, relative to the repository root, <c>/</c>-separated — the view's <c>corpus</c>.</summary>
    string RelativePath(string app);

    /// <summary>The bindings file's path, as <see cref="RelativePath"/> gives the corpus's.</summary>
    string RelativeBindingsPath(string app);

    /// <summary>The corpus text exactly as stored — a BOM included — for an edit to splice.</summary>
    string ReadText(string app);

    /// <summary>The bindings file's text exactly as stored, or null when there is none yet.</summary>
    string? ReadBindingsText(string app);

    /// <summary>Replace an app's corpus. Only ever called with a writer's validated result.</summary>
    void WriteText(string app, string text);

    /// <summary>Replace (or create) an app's bindings file. Only ever called with a writer's validated result.</summary>
    void WriteBindingsText(string app, string text);
}
