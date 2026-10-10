namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What one run of the Stage-0 gate printed, and how it exited: 0 every built behaviour is
/// named by a test, 1 it looked and something is wrong, 2 it could not look.
/// </summary>
public interface ICheckRun
{
    int ExitCode { get; }

    string Stdout { get; }

    string Stderr { get; }
}
