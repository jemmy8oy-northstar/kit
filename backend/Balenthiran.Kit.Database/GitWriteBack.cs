using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Database;

/// <summary><see cref="IGitWriteBack"/>, as <see cref="GitStore"/> reports it.</summary>
public sealed record GitWriteBack : IGitWriteBack
{
    public bool Committed { get; init; }

    public bool Pushed { get; init; }

    public string? Reason { get; init; }

    public string? Commit { get; init; }

    public string? Branch { get; init; }
}
