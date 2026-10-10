using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Cli;

/// <summary>
/// The commands, and nothing else: each one is a service in <c>Services</c>, wired by hand
/// rather than through a container, so the CLI carries no package of its own.
/// </summary>
public static class KitCli
{
    public const string Usage =
        "usage: kit <command> [args]\n\ncommands:\n"
        + "  check <app> --repo <path-to-app-repo> [--via mapping|markers] [--dir <corpus-dir>]\n"
        + "  report [<corpus-name>] [--dir <corpus-dir>]\n"
        + "  sheet [<corpus-name>] [--rev <rev>] [--dir <corpus-dir>]";

    /// <summary>Run one command. Exit 2 — could not look — for anything that is not a command.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        var rest = args.Skip(1).ToList();
        var disk = new CheckFileSystem();
        var parser = new CorpusParser();
        var resolver = new BehaviourResolver();
        ICommandRun? run = args.Count == 0 ? null : args[0] switch
        {
            "check" => new KitCheck(parser, resolver, new TestTitleReader(), disk).Run(rest),
            "report" or "sheet" => new KitReport(parser, resolver, new TestGenerator(), new ProjectReporter(), new QuestionSheet(), disk).Run(rest, sheet: args[0] == "sheet"),
            _ => null,
        };

        if (run is null)
        {
            stderr.Write(args.Count == 0 ? Usage + "\n" : $"unknown command {args[0]}\n{Usage}\n");
            return 2;
        }

        stdout.Write(run.Stdout);
        stderr.Write(run.Stderr);
        return run.ExitCode;
    }
}
