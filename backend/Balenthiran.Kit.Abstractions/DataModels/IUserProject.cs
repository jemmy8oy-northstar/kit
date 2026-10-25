namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>A repo in a user's own GitHub that carries a corpus (BEH-USERS-2): a project, with nothing Kit stores about it.</summary>
public interface IUserProject
{
    /// <summary>The repo's name, <c>snip-it</c>.</summary>
    string Name { get; }

    /// <summary>Owner and name, <c>ann-gh/snip-it</c>.</summary>
    string FullName { get; }

    /// <summary>The repo's page on GitHub.</summary>
    string Url { get; }

    /// <summary>The <c>.beh</c> files on its default branch, in path order: why it is a project.</summary>
    IReadOnlyList<string> Corpus { get; }
}
