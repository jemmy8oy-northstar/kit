using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;
using Microsoft.AspNetCore.Http.Features;

namespace Balenthiran.Kit.WebApi;

/// <summary>
/// The host: hands every request's method, RAW target, Origin and Cookie to
/// <see cref="IKitHost"/>, reads a POST's body when it says to, and delivers what it
/// returns. Deliberately one catch-all rather than an endpoint per route, so the rules
/// live in one testable function exactly as in <c>ui.js</c>, and the framework cannot add
/// an answer of its own — no 404 page, no redirect — that the router never gave.
/// </summary>
public static class KitServer
{
    /// <param name="configure">Registers services BEFORE Kit's own, which only fill what is
    /// missing — so a test can supply a session store and throttle on a fake clock.</param>
    public static WebApplication Build(string[] args, KitSettings settings, Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Node sends no `Server` header, and naming the stack helps nobody but a scanner.
        builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
        configure?.Invoke(builder.Services);
        builder.Services.AddKitServices(settings);
        var app = builder.Build();

        var host = app.Services.GetRequiredService<IKitHost>();

        app.Run(async ctx =>
        {
            // The RAW target, still percent-encoded and with its dot segments: Request.Path
            // is already partly decoded and normalised, and the host must parse it exactly
            // once, itself, the way `new URL(req.url)` does.
            var target = ctx.Features.Get<IHttpRequestFeature>()!.RawTarget;

            // Node joins a repeated header with ", " (StringValues.ToString() uses ",").
            var origin = Header(ctx.Request.Headers.Origin);
            var cookie = Header(ctx.Request.Headers.Cookie);

            var a = host.Answer(ctx.Request.Method, target, origin, cookie);
            if (a.Post is not null)
            {
                a = host.Received(a.Post, await ReadBody(ctx.Request.Body), origin, cookie);
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

    private static string? Header(Microsoft.Extensions.Primitives.StringValues values) =>
        values.Count == 0 ? null : string.Join(", ", values.ToArray());

    /// <summary>
    /// The body, or null once it runs past the limit. Past it nothing more is BUFFERED, so a
    /// large body cannot grow memory — but the rest is still drained rather than the
    /// connection dropped, so the caller gets the 413's sentence instead of a hang-up.
    /// </summary>
    private static async Task<byte[]?> ReadBody(Stream body)
    {
        using var kept = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long size = 0;
        int n;
        while ((n = await body.ReadAsync(buffer)) > 0)
        {
            size += n;
            if (size <= KitHost.MaxBody)
            {
                kept.Write(buffer, 0, n);
            }
        }

        return size > KitHost.MaxBody ? null : kept.ToArray();
    }
}
