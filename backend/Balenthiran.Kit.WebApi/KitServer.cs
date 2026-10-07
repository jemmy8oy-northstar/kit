using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Microsoft.AspNetCore.Http.Features;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// The host: hands every request's method and RAW path to <see cref="IKitRouter"/> and
/// delivers what it returns. Deliberately one catch-all rather than an endpoint per
/// route, so the routing rules live in one testable function exactly as in
/// <c>ui.js</c>, and the host cannot add an answer of its own — no framework 404
/// page, no redirect — that the router never gave.
/// </summary>
public static class KitServer
{
    public static WebApplication Build(string[] args, KitSettings settings)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddKitServices(settings);
        var app = builder.Build();

        var router = app.Services.GetRequiredService<IKitRouter>();

        // `JSON.stringify(body)`: the engine's settings, unindented.
        var json = new JsonSerializerOptions(app.Services.GetRequiredService<IEngineJsonSerialiser>().Options) { WriteIndented = false };

        app.Run(async ctx =>
        {
            // The RAW target, still percent-encoded: Request.Path is already partly
            // decoded, and the router must decode exactly once, itself.
            var target = ctx.Features.Get<IHttpRequestFeature>()!.RawTarget;
            var q = target.IndexOf('?', StringComparison.Ordinal);
            var path = q < 0 ? target : target[..q];

            var r = router.Route(ctx.Request.Method, path);
            ctx.Response.StatusCode = r.Status;
            ctx.Response.ContentType = r.ContentType;
            if (r.CacheControl is not null)
            {
                ctx.Response.Headers.CacheControl = r.CacheControl;
            }

            // Bytes from the bundle as they are; otherwise the JSON payload.
            if (r.Raw is not null)
            {
                await ctx.Response.Body.WriteAsync(r.Raw);
            }
            else
            {
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(r.Body, r.Body!.GetType(), json));
            }
        });

        return app;
    }
}
