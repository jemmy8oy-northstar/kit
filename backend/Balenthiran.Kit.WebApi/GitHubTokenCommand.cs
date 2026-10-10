using Balenthiran.Kit.Abstractions.Exceptions;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// <c>dotnet Balenthiran.Kit.WebApi.dll github-token</c>: print a token from the same source the
/// server uses, and nothing else. The entrypoint clones with it (kit#88) — a shell cannot sign the
/// App's JWT, and the clone happens before the server starts. Exit 1, with the reason on stderr and
/// nothing on stdout, when there is no credential or it could not be exchanged, so the entrypoint
/// falls back to write-back OFF rather than cloning with an empty password.
/// </summary>
public static class GitHubTokenCommand
{
    public const string Name = "github-token";

    public static async Task<int> RunAsync(KitSettings settings, TextWriter stdout, TextWriter stderr, HttpMessageHandler? handler = null)
    {
        using var http = new HttpClient(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null) { Timeout = TimeSpan.FromSeconds(20) };
        var source = ServiceRegistration.TokenSource(settings, http, TimeProvider.System);
        try
        {
            var token = await source.TokenAsync();
            if (token is null)
            {
                await stderr.WriteLineAsync($"kit {Name}: no credential is configured — set KIT_GIT_TOKEN, or all three KIT_GITHUB_APP_ID, KIT_GITHUB_INSTALLATION_ID and KIT_GITHUB_PRIVATE_KEY");
                return 1;
            }

            await stdout.WriteAsync(token);
            return 0;
        }
        catch (GitHubTokenException e)
        {
            await stderr.WriteLineAsync($"kit {Name}: {e.Message}");
            return 1;
        }
        finally
        {
            (source as IDisposable)?.Dispose();
        }
    }
}
