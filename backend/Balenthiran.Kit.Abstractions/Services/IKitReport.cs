using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// <c>kit report</c> and <c>kit sheet</c> — the port of <c>kit.js</c>'s command line. The report
/// prints every generated test and the measurements; the sheet renders the question sheet a
/// human answers. Output is Node's byte for byte, but for the command's own name.
/// </summary>
public interface IKitReport
{
    /// <summary>Run over the arguments after the command. Relative paths resolve against the process's directory.</summary>
    ICommandRun Run(IReadOnlyList<string> args, bool sheet);
}
