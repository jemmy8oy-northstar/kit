using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// Wires the engine, the router and the host. All singletons: the engine is stateless, and
/// the session store and throttle MUST be one per server — a throttle per request throttles nothing.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddKitServices(this IServiceCollection services, KitSettings settings)
    {
        services.AddSingleton(settings);
        var disk = new CorpusDirectory(settings.Dir, settings.RepoRoot);
        if (settings.Projects.Count == 0)
        {
            services.AddSingleton<ICorpusDirectory>(disk);
        }
        else
        {
            // BEH-PULL-1 (kit#88): reads from GitHub, writes to the clone on disk.
            services.AddSingleton(sp => new GitHubCorpusDirectory(
                disk,
                settings.Projects,
                new GitHubCorpusReader(GitHubClient(sp), sp.GetRequiredService<IGitHubTokenSource>())));
            services.AddSingleton<ICorpusDirectory>(sp => sp.GetRequiredService<GitHubCorpusDirectory>());
            services.AddHostedService<ProjectPoller>();
        }

        services.AddSingleton<ICorpusParser, CorpusParser>();
        services.AddSingleton<IBehaviourResolver, BehaviourResolver>();
        services.AddSingleton<ITestGenerator, TestGenerator>();
        services.AddSingleton<IProjectReporter, ProjectReporter>();
        services.AddSingleton<IEngineJsonSerialiser, EngineJsonSerialiser>();
        services.AddSingleton<IProjectViewer, ProjectViewer>();
        services.AddSingleton<ICorpusWriter, CorpusWriter>();
        services.AddSingleton<IUiBundle>(new UiBundle(settings.Dist));
        services.AddSingleton<IUrlParser, UrlParser>();
        services.AddSingleton<IOriginPolicy>(sp => new OriginPolicy(sp.GetRequiredService<IUrlParser>(), settings.PublicOrigin));
        services.TryAddSingleton<ISessionStore>(_ => new SessionStore());
        services.TryAddSingleton<ISignInThrottle>(_ => new SignInThrottle());
        services.AddSingleton<IGitStore>(sp => new GitStore(settings.Git, settings.GitRemote, settings.GitBranch, baseBranch: settings.GitBase, tokens: sp.GetRequiredService<IGitHubTokenSource>()));

        // A test registers a scripted HttpMessageHandler in front of GitHub, so it scores THIS wiring
        // (token, repository, base) by the request GitHub would have received.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(sp => TokenSource(settings, GitHubClient(sp), sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IPullRequestOpener>(sp => new GitHubPullRequestOpener(
            GitHubClient(sp),
            sp.GetRequiredService<IGitHubTokenSource>(),
            settings.GitRepository));
        services.AddSingleton<IKitRouter>(sp => new KitRouter(
            sp.GetRequiredService<ICorpusDirectory>(),
            sp.GetRequiredService<IProjectViewer>(),
            sp.GetRequiredService<IUiBundle>(),
            settings.Password,
            sp.GetRequiredService<IOriginPolicy>(),
            sp.GetRequiredService<ISessionStore>(),
            sp.GetRequiredService<ISignInThrottle>(),
            settings.Host,
            settings.Secure,
            sp.GetRequiredService<ICorpusWriter>(),
            sp.GetRequiredService<IGitStore>(),
            deployed: settings.PublicOrigin is not null,
            pulls: sp.GetRequiredService<IPullRequestOpener>(),
            head: settings.GitBranch,
            baseBranch: settings.GitBase));
        services.AddSingleton<IKitHost>(sp => new KitHost(
            sp.GetRequiredService<IKitRouter>(),
            sp.GetRequiredService<IUrlParser>(),
            sp.GetRequiredService<IEngineJsonSerialiser>(),
            sp.GetRequiredService<IOriginPolicy>(),
            settings.BasePath));
        return services;
    }

    /// <summary>The App installation when all three are set, else <c>KIT_GIT_TOKEN</c> — shared by the server and <c>github-token</c>.</summary>
    public static IGitHubTokenSource TokenSource(KitSettings settings, HttpClient http, TimeProvider clock) => settings.GitHubApp
        ? new GitHubAppTokenSource(http, settings.GitHubAppId!, settings.GitHubInstallationId!, settings.PrivateKey()!, clock)
        : new StaticGitHubTokenSource(settings.Token());

    private static HttpClient GitHubClient(IServiceProvider sp) =>
        new(sp.GetService<HttpMessageHandler>() ?? new SocketsHttpHandler()) { Timeout = TimeSpan.FromSeconds(20) };
}
