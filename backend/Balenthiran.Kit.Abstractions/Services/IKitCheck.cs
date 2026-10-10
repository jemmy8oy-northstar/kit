using Balenthiran.Kit.Abstractions.DataModels;

namespace Balenthiran.Kit.Abstractions.Services;

/// <summary>
/// <c>kit check</c> — the Stage-0 gate: a behaviour with no test naming it fails the build.
/// The port of <c>check.js</c>; its output is that file's, byte for byte.
/// </summary>
public interface IKitCheck
{
    /// <summary>Run the gate over the arguments after <c>check</c>. Relative paths resolve against the process's directory.</summary>
    ICommandRun Run(IReadOnlyList<string> args);
}
