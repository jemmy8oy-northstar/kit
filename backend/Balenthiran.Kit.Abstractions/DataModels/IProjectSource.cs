namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// Where on GitHub a set of corpora lives (BEH-PULL-1): one directory of one branch of one
/// repository, written <c>owner/repo@branch:path</c> in <c>KIT_PROJECTS</c>.
/// </summary>
public interface IProjectSource
{
    string Owner { get; }

    string Repository { get; }

    string Branch { get; }

    /// <summary>The directory holding <c>&lt;app&gt;.beh</c>, relative to the repository root, <c>/</c>-separated, no leading or trailing slash.</summary>
    string Path { get; }
}
