using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-6</c> (kit#803): <c>KIT_BOT_READ_TOKEN</c> lets the bot read with no
/// session, GET only. Every case runs locked, so a pass cannot come from an open server.
/// </summary>
public class BotReadKeyTests
{
    private const string Key = "bot-read-secret";

    [Fact]
    public void The_key_reads_runs_and_a_project_as_the_operator()
    {
        var router = Router(Key);

        Assert.Equal(200, router.Route("GET", "/api/runs", bearer: $"Bearer {Key}").Status);
        Assert.Equal(200, router.Route("GET", "/api/projects/kit", bearer: $"Bearer {Key}").Status);
        Assert.Equal(200, router.Route("GET", "/api/projects", bearer: $"Bearer {Key}").Status);
    }

    [Fact]
    public void A_wrong_or_empty_bearer_is_the_same_401_as_none()
    {
        var router = Router(Key);
        var none = router.Route("GET", "/api/runs");
        Assert.Equal(401, none.Status);

        foreach (var header in new[] { "Bearer nope", "Bearer ", "Bearer", "", Key, $"Basic {Key}" })
        {
            var refused = router.Route("GET", "/api/runs", bearer: header);
            Assert.Equal(401, refused.Status);
            Assert.Equal("not-signed-in", Assert.IsType<ApiError>(refused.Body).Error);
            Assert.Equal(Assert.IsType<ApiError>(none.Body).Reason, Assert.IsType<ApiError>(refused.Body).Reason);
        }
    }

    [Fact]
    public void With_no_key_set_a_bearer_changes_nothing()
    {
        foreach (var setting in new string?[] { null, "", "   " })
        {
            var router = Router(setting);
            Assert.Equal(401, router.Route("GET", "/api/runs", bearer: $"Bearer {Key}").Status);
            Assert.Equal(401, router.Route("GET", "/api/runs", bearer: "Bearer ").Status);
        }
    }

    [Fact]
    public void The_key_never_writes()
    {
        var router = Router(Key);
        var body = JsonSerializer.SerializeToElement(new { });

        // The same answer as with no bearer at all, for a write the lock refuses.
        var without = router.Route("POST", "/api/commit", body: body);
        var with = router.Route("POST", "/api/commit", body: body, bearer: $"Bearer {Key}");
        Assert.Equal(401, without.Status);
        Assert.Equal(without.Status, with.Status);
        Assert.Equal(JsonSerializer.Serialize(without.Body), JsonSerializer.Serialize(with.Body));

        // A run start is a write too.
        Assert.Equal(401, router.Route("POST", "/api/runs", body: body, bearer: $"Bearer {Key}").Status);
        Assert.Equal(401, router.Route("POST", "/api/session", body: JsonSerializer.SerializeToElement(new { password = Key }), bearer: $"Bearer {Key}").Status);
    }

    [Fact]
    public void The_key_does_not_pass_as_a_cookie()
    {
        var router = Router(Key);
        Assert.Equal(401, router.Route("GET", "/api/runs", cookie: $"kit_session={Key}").Status);
    }

    [Fact]
    public void A_session_cookie_wins_over_the_bearer()
    {
        var router = Router(Key);
        var him = Cookie(router);

        // Both present: served as the cookie's identity.
        Assert.Equal(200, router.Route("GET", "/api/runs", him, bearer: "Bearer wrong").Status);
        Assert.Equal(200, router.Route("GET", "/api/runs", him, bearer: $"Bearer {Key}").Status);

        // The cookie's own session state is untouched by the bearer.
        var state = Assert.IsType<SessionState>(router.Route("GET", "/api/session", him, bearer: $"Bearer {Key}").Body);
        Assert.True(state.SignedIn);
    }

    [Fact]
    public void Another_users_cookie_is_not_lifted_to_the_operator_by_the_bearer()
    {
        // Operator-on-operator above passes whichever wins; Runs is the operator's alone, so this cannot.
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash("ann-pw", iterations: 1000));
        var router = Router(Key, users);
        var body = JsonSerializer.SerializeToElement(new { username = "ann", password = "ann-pw" });
        var ann = router.Route("POST", "/api/session", body: body).SetCookie!.Split(';')[0];

        Assert.Equal(403, router.Route("GET", "/api/runs", ann).Status);
        Assert.Equal(403, router.Route("GET", "/api/runs", ann, bearer: $"Bearer {Key}").Status);
        Assert.Equal(200, router.Route("GET", "/api/runs", bearer: $"Bearer {Key}").Status);
    }

    private static string Cookie(KitRouter router)
    {
        var body = JsonSerializer.SerializeToElement(new { password = "pw" });
        return router.Route("POST", "/api/session", body: body).SetCookie!.Split(';')[0];
    }

    private static KitRouter Router(string? botKey, AccountsTests.MemoryUsers? users = null)
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            "pw",
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            users: users,
            botReadToken: botKey);
    }
}
