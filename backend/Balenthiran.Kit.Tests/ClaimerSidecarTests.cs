using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c>runner/claimer.js</c>, the sidecar that alone holds the runner pool's token (kit#701), run for real under
/// node against a fake Kit. The claim it relays is the one the server itself serialises for a PoolClaim, and the
/// assertions are about what Kit receives (which credential on which route) and what a runner can get back.
/// </summary>
public sealed class ClaimerSidecarTests : IDisposable
{
    private readonly HttpListener _kit;
    private readonly string _kitUrl;
    private readonly List<(string Path, string? Pool, string? Key, string Body)> _seen = [];
    private readonly Queue<(int Status, string Body)> _claims = new();
    private int _finishedStatus = 200;
    private Process? _claimer;
    private string _claimerUrl = "";
    private readonly CancellationTokenSource _stop = new();

    public ClaimerSidecarTests()
    {
        (_kit, var port) = BoundListener();
        _kitUrl = $"http://127.0.0.1:{port}/kit";
        _ = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext c;
                try { c = await _kit.GetContextAsync(); } catch { return; }
                var body = await new StreamReader(c.Request.InputStream).ReadToEndAsync();
                (int Status, string Body) answer;
                lock (_seen)
                {
                    var path = c.Request.Url!.AbsolutePath;
                    _seen.Add((path, c.Request.Headers["x-kit-runner-pool"], c.Request.Headers["x-api-key"], body));
                    answer = path.EndsWith("/claim") ? (_claims.Count > 0 ? _claims.Dequeue() : (204, "")) : (_finishedStatus, "");
                }
                c.Response.StatusCode = answer.Status;
                var bytes = Encoding.UTF8.GetBytes(answer.Body);
                await c.Response.OutputStream.WriteAsync(bytes);
                c.Response.Close();
            }
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        _kit.Close();
        if (_claimer is { HasExited: false }) { _claimer.Kill(); _claimer.WaitForExit(); }
        _claimer?.Dispose();
    }

    /// <summary>
    /// The port is free when asked and released before anything binds it, so a test running in parallel can take
    /// it in between (kit CI on 2026-11-20: "Address already in use"). Whoever binds it must try another on a clash.
    /// </summary>
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private const int BindAttempts = 10;

    private static (HttpListener Listener, int Port) BoundListener()
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = FreePort();
            var l = new HttpListener();
            l.Prefixes.Add($"http://127.0.0.1:{port}/");
            try { l.Start(); return (l, port); }
            catch (HttpListenerException) when (attempt < BindAttempts) { l.Close(); }
        }
    }

    private static string ClaimJson(string id, string token) =>
        new EngineJsonSerialiser().Serialise(new PoolClaim(id, token, "o/r", "t", "", "sonnet", null));

    /// <summary>
    /// Waits for the claimer's own "listening" line, its only stdout. Answering a GET is not proof: on a port it
    /// lost, the GET can reach another test's listener while the claimer dies with EADDRINUSE.
    /// </summary>
    private void StartClaimer(string? env = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = FreePort();
            var psi = new ProcessStartInfo("node", [Path.Combine(RepoLayout.Root, "runner", "claimer.js")])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment.Clear();
            psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH")!;
            psi.Environment["KIT_URL"] = _kitUrl;
            psi.Environment["KIT_RUNNER_POOL_TOKEN"] = "pool-secret";
            psi.Environment["KIT_CLAIMER_PORT"] = port.ToString();
            if (env is not null) psi.Environment["KIT_CLAIM_MAX_SECONDS"] = env;
            _claimer = Process.Start(psi)!;
            _claimerUrl = $"http://127.0.0.1:{port}";
            var line = _claimer.StandardOutput.ReadLineAsync();
            if (line.Wait(TimeSpan.FromSeconds(10)) && line.Result?.StartsWith("kit-claimer: listening") == true) return;
            if (!_claimer.HasExited) _claimer.Kill();
            _claimer.WaitForExit();
            var stderr = _claimer.StandardError.ReadToEnd();
            _claimer.Dispose();
            _claimer = null;
            if (attempt == BindAttempts) throw new InvalidOperationException($"claimer never listened: {stderr}");
        }
    }

    private async Task<(int Status, string Body)> Post(string path, string body = "")
    {
        using var http = new HttpClient();
        var r = await http.PostAsync(_claimerUrl + path, new StringContent(body, Encoding.UTF8, "application/json"));
        return ((int)r.StatusCode, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_claim_is_made_with_the_pool_token_and_handed_over_exactly_as_Kit_wrote_it()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        StartClaimer();

        var (status, body) = await Post("/next");

        Assert.Equal(200, status);
        Assert.Equal(ClaimJson("r1", "run-tok-1"), body);
        var claim = Assert.Single(_seen);
        Assert.EndsWith("/api/runs/claim", claim.Path);
        Assert.Equal("pool-secret", claim.Pool);
    }

    [Fact]
    public async Task Nothing_queued_is_a_204_and_leaves_the_sidecar_free_to_ask_again()
    {
        StartClaimer();

        Assert.Equal(204, (await Post("/next")).Status);
        Assert.Equal(204, (await Post("/next")).Status);
    }

    [Fact]
    public async Task Asking_again_while_a_claim_is_unreported_releases_nothing_and_never_reaches_Kit()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        _claims.Enqueue((200, ClaimJson("r2", "someone-elses-token")));
        StartClaimer();
        await Post("/next");

        var (status, body) = await Post("/next");

        Assert.Equal(409, status);
        Assert.DoesNotContain("someone-elses-token", body);
        Assert.Single(_seen); // Kit was asked once, so the second queued run is untouched
    }

    [Fact]
    public async Task The_report_goes_to_Kit_under_the_runs_own_token_never_the_pool_token_and_frees_the_next_claim()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        _claims.Enqueue((200, ClaimJson("r2", "run-tok-2")));
        StartClaimer();
        await Post("/next");

        var finished = await Post("/finished", "{\"status\":\"finished\"}");

        Assert.Equal(200, finished.Status);
        var report = _seen[1];
        Assert.EndsWith("/api/runs/finished", report.Path);
        Assert.Equal("run-tok-1", report.Key);
        Assert.Null(report.Pool);
        Assert.Equal("{\"status\":\"finished\"}", report.Body);
        Assert.Equal(200, (await Post("/next")).Status);
    }

    [Fact]
    public async Task A_report_Kit_refuses_is_relayed_and_the_claim_stays_outstanding_for_the_retry()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        StartClaimer();
        await Post("/next");
        _finishedStatus = 400;

        Assert.Equal(400, (await Post("/finished", "{\"status\":\"finished\",\"usage\":{}}")).Status);
        Assert.Equal(409, (await Post("/next")).Status);
        _finishedStatus = 200;
        Assert.Equal(200, (await Post("/finished", "{\"status\":\"finished\"}")).Status);
    }

    [Fact]
    public async Task A_report_with_no_claim_outstanding_is_refused_and_not_forwarded()
    {
        StartClaimer();

        Assert.Equal(409, (await Post("/finished", "{\"status\":\"finished\"}")).Status);
        Assert.Empty(_seen);
    }

    [Fact]
    public async Task A_claim_never_reported_is_reported_failed_when_the_pod_is_stopped()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        StartClaimer();
        await Post("/next");

        Kill("SIGTERM");

        Assert.True(_claimer!.WaitForExit(10000), "the sidecar must exit on SIGTERM");
        var report = _seen[1];
        Assert.EndsWith("/api/runs/finished", report.Path);
        Assert.Equal("run-tok-1", report.Key);
        Assert.Equal("{\"status\":\"failed\",\"failure\":\"pod stopped\"}", report.Body);
    }

    [Fact]
    public async Task A_claim_unreported_past_the_limit_is_reported_failed_and_the_pod_claims_again()
    {
        _claims.Enqueue((200, ClaimJson("r1", "run-tok-1")));
        _claims.Enqueue((200, ClaimJson("r2", "run-tok-2")));
        StartClaimer(env: "1");
        await Post("/next");
        await Task.Delay(1300);

        var next = await Post("/next");

        Assert.Equal(200, next.Status);
        Assert.Contains("r2", next.Body);
        Assert.Contains(_seen, s => s.Path.EndsWith("/finished") && s.Key == "run-tok-1" && s.Body.Contains("failed"));
    }

    [Fact]
    public async Task Without_the_pool_token_the_sidecar_refuses_to_start()
    {
        var psi = new ProcessStartInfo("node", [Path.Combine(RepoLayout.Root, "runner", "claimer.js")]) { RedirectStandardError = true };
        psi.Environment.Clear();
        psi.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH")!;
        psi.Environment["KIT_URL"] = _kitUrl;
        using var p = Process.Start(psi)!;
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        Assert.Equal(64, p.ExitCode);
        Assert.Contains("KIT_RUNNER_POOL_TOKEN", err);
    }

    private void Kill(string signal)
    {
        using var k = Process.Start(new ProcessStartInfo("kill", ["-" + signal, _claimer!.Id.ToString()]))!;
        k.WaitForExit();
    }
}
