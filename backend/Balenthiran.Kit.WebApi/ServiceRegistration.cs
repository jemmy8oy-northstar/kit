using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.WebApi;

/// <summary>Wires the engine and the router. Every service is stateless, so all are singletons.</summary>
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
        services.AddSingleton<IKitRouter>(sp => new KitRouter(
            sp.GetRequiredService<ICorpusDirectory>(),
            sp.GetRequiredService<IProjectViewer>(),
            settings.Password));
        return services;
    }
}
