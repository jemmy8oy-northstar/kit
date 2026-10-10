namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>One file of a <see cref="ICorpusSnapshot"/>. The blob sha names its content, so an unchanged sha is never fetched twice.</summary>
public interface ISnapshotFile
{
    string Sha { get; }

    /// <summary>The file exactly as stored — a BOM included — as <c>ICorpusDirectory.ReadText</c> must give it to a splice.</summary>
    string Text { get; }
}
