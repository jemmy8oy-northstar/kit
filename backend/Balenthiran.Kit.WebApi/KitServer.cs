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

        // BEH-PULL-5 (kit#300): GitHub is read when a request finds the last read expired. Only
        // the API reads projects; the bundle's files never wait on GitHub. ⚠️ Deployed, the ingress
        // does NOT strip KIT_BASE_PATH, so the API is `/kit/api/…` there: match under the prefix.
        var poller = app.Services.GetService<ProjectPoller>();
        var api = settings.BasePath + "/api/";

        // BEH-NOTIFY-1: the agent's feed is read the same way, when its page asks and the last read expired.
        var feed = app.Services.GetRequiredService<NotificationReader>();
        var feedRefresh = new ExpiringRefresh(() => feed.RefreshAsync(), settings.ProjectsPoll, app.Services.GetRequiredService<TimeProvider>());
        var feedRoute = api + "notifications";

        // BEH-USERS-9: the metered model proxy. A stream cannot be an IKitResponse, so it is mounted here,
        // ahead of the router, and answers only on these two paths.
        var proxy = app.Services.GetRequiredService<AgentProxy>();
        var proxyPath = api + "agent/v1/messages";

        // BEH-USERS-9: the runner pool's two machine routes. No session, Origin or cookie applies to them: a
        // pod proves itself with the pool token (claim) or its own run token (finished). Mounted only when
        // KIT_RUNNER_POOL_TOKEN is set; without it these paths fall through to the router's 404.
        var poolRoutes = app.Services.GetService<RunnerPoolRoutes>();
        var claimPath = api + "runs/claim";
        var finishedPath = api + "runs/finished";
        var poolJson = app.Services.GetRequiredService<IEngineJsonSerialiser>().Options;

        app.Run(async ctx =>
        {
            // The RAW target, still percent-encoded and with its dot segments: Request.Path
            // is already partly decoded and normalised, and the host must parse it exactly
            // once, itself, the way `new URL(req.url)` does.
            var target = ctx.Features.Get<IHttpRequestFeature>()!.RawTarget;

            // Node joins a repeated header with ", " (StringValues.ToString() uses ",").
            var origin = Header(ctx.Request.Headers.Origin);
            var cookie = Header(ctx.Request.Headers.Cookie);

            var path = target.Split('?')[0];
            if (path == proxyPath || path == proxyPath + "/count_tokens")
            {
                await ServeAgent(ctx, proxy, countTokens: path != proxyPath);
                return;
            }

            if (poolRoutes is not null && (path == claimPath || path == finishedPath))
            {
                await ServePool(ctx, poolRoutes, path == claimPath, poolJson);
                return;
            }

            if (poller is not null && target.StartsWith(api, StringComparison.Ordinal))
            {
                await poller.EnsureFreshAsync(ctx.RequestAborted);
            }

            if ((settings.AgentFeed is not null || settings.AgentSchedule is not null) && target.Split('?')[0] == feedRoute)
            {
                await feedRefresh.EnsureFreshAsync(ProjectPoller.Wait, ctx.RequestAborted);
            }

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

    private static async Task ServePool(HttpContext ctx, RunnerPoolRoutes routes, bool claim, System.Text.Json.JsonSerializerOptions options)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method))
        {
            ctx.Response.StatusCode = 405;
            ctx.Response.Headers.Allow = "POST";
            ctx.Response.Headers.CacheControl = "no-store";
            return;
        }

        Balenthiran.Kit.DataModels.Models.KitResponse answer;
        if (claim)
        {
            answer = await routes.ClaimAsync(Header(ctx.Request.Headers[RunnerPoolRoutes.PoolHeader]), ctx.RequestAborted);
        }
        else
        {
            System.Text.Json.JsonElement? body = null;
            if (await ReadBody(ctx.Request.Body) is { } bytes && bytes.Length > 0)
            {
                try
                {
                    body = System.Text.Json.JsonDocument.Parse(bytes).RootElement.Clone();
                }
                catch (System.Text.Json.JsonException)
                {
                    // Left null: the route answers 400 for a body it cannot read.
                }
            }

            answer = routes.Finished(Header(ctx.Request.Headers["x-api-key"]), body);
        }

        ctx.Response.StatusCode = answer.Status;
        ctx.Response.ContentType = answer.ContentType;
        if (answer.CacheControl is not null)
        {
            ctx.Response.Headers.CacheControl = answer.CacheControl;
        }

        if (answer.Body is not null)
        {
            await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(answer.Body, answer.Body.GetType(), options));
        }
    }

    /// <summary>
    /// One call to the model proxy: the run token is the <c>x-api-key</c>, the body is relayed as it is, and the
    /// answer is written and flushed as it arrives, with the proxy buffer switched off for an event stream.
    /// </summary>
    private static async Task ServeAgent(HttpContext ctx, AgentProxy proxy, bool countTokens)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method))
        {
            ctx.Response.StatusCode = 405;
            ctx.Response.Headers.Allow = "POST";
            ctx.Response.Headers.CacheControl = "no-store";
            await ctx.Response.WriteAsync("""{"type":"error","error":{"type":"invalid_request_error","message":"the model proxy takes POST"}}""");
            return;
        }

        Stream Begin(int status, string contentType)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            if (contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers.CacheControl = "no-cache";
                ctx.Response.Headers["X-Accel-Buffering"] = "no";
            }
            else
            {
                ctx.Response.Headers.CacheControl = "no-store";
            }

            return ctx.Response.Body;
        }

        // The token is checked BEFORE the body is read, so a caller Kit does not know cannot make it buffer 32 MB.
        var token = Header(ctx.Request.Headers["x-api-key"]);
        if (await proxy.RefuseEarlyAsync(token, countTokens, Begin, ctx.RequestAborted))
        {
            return;
        }

        // Kestrel's own limit (30,000,000) is below the proxy's, so raise it for this endpoint only, by one
        // byte, and the proxy's own 413 answers in Anthropic's error shape rather than Kestrel's bare one.
        if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = AgentProxy.MaxBody + 1L;
        }

        byte[]? body;
        try
        {
            body = await ReadBody(ctx.Request.Body, AgentProxy.MaxBody, ctx.RequestAborted);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // The caller hung up before a call existed: nothing was sent upstream, so nothing was spent.
            return;
        }

        var call = new AgentCall(
            token,
            Header(ctx.Request.Headers["anthropic-version"]),
            Header(ctx.Request.Headers["anthropic-beta"]),
            countTokens,
            body);
        await proxy.ServeAsync(call, Begin, ctx.RequestAborted);
    }

    private static string? Header(Microsoft.Extensions.Primitives.StringValues values) =>
        values.Count == 0 ? null : string.Join(", ", values.ToArray());

    /// <summary>
    /// The body, or null once it runs past the limit. Past it nothing more is BUFFERED, so a
    /// large body cannot grow memory — but the rest is still drained rather than the
    /// connection dropped, so the caller gets the 413's sentence instead of a hang-up.
    /// </summary>
    private static Task<byte[]?> ReadBody(Stream body) => ReadBody(body, KitHost.MaxBody);

    private static async Task<byte[]?> ReadBody(Stream body, int limit, CancellationToken cancellationToken = default)
    {
        using var kept = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long size = 0;
        int n;
        while ((n = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            size += n;
            if (size <= limit)
            {
                kept.Write(buffer, 0, n);
            }
        }

        return size > limit ? null : kept.ToArray();
    }
}
