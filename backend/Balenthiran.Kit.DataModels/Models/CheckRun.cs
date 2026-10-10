using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.DataModels.Models;

/// <summary>What one run of the Stage-0 gate printed, and how it exited.</summary>
public sealed class CheckRun : ICheckRun
{
    public required int ExitCode { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}
