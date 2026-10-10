namespace Balenthiran.Kit.Abstractions.DataModels;

/// <summary>
/// What one run of a <c>kit</c> command printed, and how it exited: 0 it looked and all is
/// well, 1 it looked and something is wrong, 2 it could not look.
/// </summary>
public interface ICommandRun
{
    int ExitCode { get; }

    string Stdout { get; }

    string Stderr { get; }
}
