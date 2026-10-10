using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What one run of a `kit` command printed, and how it exited.</summary>
public sealed class CommandRun : ICommandRun
{
    public required int ExitCode { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}
