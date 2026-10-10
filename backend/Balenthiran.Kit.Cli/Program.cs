using System.Text;
using Balenthiran.Kit.Cli;

// Node writes UTF-8 whatever the terminal says; the gate's output has ✅ ✗ ◌ in it.
var utf8 = new UTF8Encoding(false);
using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8);
using var stderr = new StreamWriter(Console.OpenStandardError(), utf8);
var code = KitCli.Run(args, stdout, stderr);
stdout.Flush();
stderr.Flush();
return code;
