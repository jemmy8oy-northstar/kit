using Balenthiran.Kit.Abstractions.Services;
using Microsoft.AspNetCore.Http.Features;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// The host: hands every request's method, RAW target and Origin to <see cref="IKitHost"/>
/// and delivers what it returns. Deliberately one catch-all rather than an endpoint per
/// route, so the routing rules live in one testable function exactly as in
/// <c>ui.js</c>, and the framework cannot add an answer of its own — no 404 page, no
/// redirect — that the router never gave.
/// </summary>
public static class KitServer
{
    public static WebApplication Build(string[] args, KitSettings settings)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Node sends no `Server` header, and naming the stack helps nobody but a scanner.
        builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
        builder.Services.AddKitServices(settings);
        var app = builder.Build();

        var host = app.Services.GetRequiredService<IKitHost>();
        var router = app.Services.GetRequiredService<IKitRouter>();

        app.Run(async ctx =>
        {
            // The RAW target, still percent-encoded and with its dot segments: Request.Path
            // is already partly decoded and normalised, and the host must parse it exactly
            // once, itself, the way `new URL(req.url)` does.
            var target = ctx.Features.Get<IHttpRequestFeature>()!.RawTarget;

            // Node joins a repeated Origin with ", " (StringValues.ToString() uses ",").
            var origins = ctx.Request.Headers.Origin;
            var origin = origins.Count == 0 ? null : string.Join(", ", origins.ToArray());

            var a = host.Answer(ctx.Request.Method, target, origin);

            // A POST under the prefix: the write half is not ported, so the router answers it.
            if (a.Post is not null)
            {
                a = host.Deliver(router.Route("POST", a.Post), origin);
            }

            ctx.Response.StatusCode = a.Status;
            foreach (var (k, v) in a.Headers)
            {
                ctx.Response.Headers[k] = v;
            }

            if (a.Raw is not null)
            {
                await ctx.Response.Body.WriteAsync(a.Raw);
            }
            else if (!string.IsNullOrEmpty(a.Body))
            {
                await ctx.Response.WriteAsync(a.Body);
            }
        });

        return app;
    }
}
