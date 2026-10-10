using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Cli;

/// <summary>
/// The commands, and nothing else: each one is a service in <c>Services</c>, wired by hand
/// rather than through a container, so the CLI carries no package of its own.
/// </summary>
public static class KitCli
{
    public const string Usage = "usage: kit <command> [args]\n\ncommands:\n  check <app> --repo <path-to-app-repo> [--via mapping|markers] [--dir <corpus-dir>]";

    /// <summary>Run one command. Exit 2 — could not look — for anything that is not a command.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Count > 0 && args[0] == "check")
        {
            IKitCheck check = new KitCheck(new CorpusParser(), new BehaviourResolver(), new TestTitleReader(), new CheckFileSystem());
            var run = check.Run(args.Skip(1).ToList());
            stdout.Write(run.Stdout);
            stderr.Write(run.Stderr);
            return run.ExitCode;
        }

        stderr.Write(args.Count == 0 ? Usage + "\n" : $"unknown command {args[0]}\n{Usage}\n");
        return 2;
    }
}
