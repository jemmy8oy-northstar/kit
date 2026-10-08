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
        services.AddSingleton<ICorpusDirectory>(new CorpusDirectory(settings.Dir, settings.RepoRoot));
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
            sp.GetRequiredService<ICorpusWriter>()));
        services.AddSingleton<IKitHost>(sp => new KitHost(
            sp.GetRequiredService<IKitRouter>(),
            sp.GetRequiredService<IUrlParser>(),
            sp.GetRequiredService<IEngineJsonSerialiser>(),
            sp.GetRequiredService<IOriginPolicy>(),
            settings.BasePath));
        return services;
    }
}
