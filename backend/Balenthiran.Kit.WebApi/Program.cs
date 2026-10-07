using Balenthiran.Kit.WebApi;

KitServer.Build(args, KitSettings.FromEnvironment(Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory())).Run();

// Exposed so a test can name the entry assembly (top-level statements make Program internal).
public partial class Program;
