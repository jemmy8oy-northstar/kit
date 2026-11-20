using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-hosted.beh <c>BEH-GATE-H1</c> (kit#795): nothing of Kit is read signed out, whatever repo a
/// project lives in. His words: "none of kit should be accessible to anyone that's not signed in".
/// Every case runs locked, because an open read on a server with no password proves nothing about
/// the lock.
/// </summary>
public class ReadGateTests
{
    private static readonly string[] Gated =
    [
        "/api/projects",
        "/api/projects/kit",
        "/api/projects/snip-it",
        "/api/projects/kit/notes",
        "/api/projects/kit/schedule",
        "/api/projects/kit/pending",
        "/api/projects/kit/history",
        "/api/projects/no-such-app",
        "/api/activity",
        "/api/notifications",
        "/api/settings/features",
        "/api/settings/wake",
    ];

    [Fact]
    public void Locked_every_read_needs_a_session()
    {
        var router = Router(password: "pw");

        // The lock is genuinely on: without this the reads below would pass on an unlocked server.
        Assert.Equal(401, router.Route("POST", "/api/commit").Status);

        foreach (var path in Gated)
        {
            var refused = router.Route("GET", path);
            Assert.True(refused.Status == 401, $"{path} answered {refused.Status} signed out");
            Assert.Equal("not-signed-in", Assert.IsType<ApiError>(refused.Body).Error);
        }

        // Signed in, the same reads are served: the gate is on the caller, not the project.
        var him = Cookie(router);
        Assert.Equal(200, router.Route("GET", "/api/projects/kit", him).Status);
        Assert.Equal(200, router.Route("GET", "/api/activity", him).Status);
        Assert.Contains("kit", Apps(router.Route("GET", "/api/projects", him)));
        Assert.Equal(404, router.Route("GET", "/api/projects/no-such-app", him).Status);
    }

    [Fact]
    public void What_a_machine_reads_signed_out_stays_open()
    {
        // The agent reads its idle choice and the runner fetches the process, both with no session.
        var router = Router(password: "pw");

        foreach (var path in new[] { "/api/health", "/api/session", "/api/settings/idle", "/api/process" })
        {
            Assert.Equal(200, router.Route("GET", path).Status);
        }
    }

    [Fact]
    public void Unlocked_loopback_reads_everything_and_any_other_interface_reads_nothing()
    {
        Assert.Equal(200, Router(password: null).Route("GET", "/api/projects/kit").Status);
        Assert.Equal(200, Router(password: null).Route("GET", "/api/activity").Status);

        var routable = Router(password: null, host: "0.0.0.0");
        Assert.Equal(401, routable.Route("GET", "/api/projects/kit").Status);
        Assert.Equal(401, routable.Route("GET", "/api/projects").Status);
    }

    private static List<string> Apps(IKitResponse r) =>
        Assert.IsType<ProjectList>(r.Body).Projects
            .Select(p => System.Text.Json.JsonSerializer.SerializeToElement(p).GetProperty("App").GetString()!).ToList();

    private static string Cookie(KitRouter router)
    {
        var body = System.Text.Json.JsonSerializer.SerializeToElement(new { password = "pw" });
        var signedIn = router.Route("POST", "/api/session", body: body);
        return signedIn.SetCookie!.Split(';')[0];
    }

    private static KitRouter Router(string? password, string host = "127.0.0.1")
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            password,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            host: host);
    }
}
