using Balenthiran.Kit.WebApi;

var settings = KitSettings.FromEnvironment(Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory());
if (args is [GitHubTokenCommand.Name])
{
    return await GitHubTokenCommand.RunAsync(settings, Console.Out, Console.Error);
}

KitServer.Build(args, settings).Run();
return 0;

// Exposed so a test can name the entry assembly (top-level statements make Program internal).
public partial class Program;
