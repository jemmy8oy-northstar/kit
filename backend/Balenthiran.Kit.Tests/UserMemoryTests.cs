using System.Text.Json;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-USERS-4</c>: every user's agent gets the same generic dev process, and each
/// user's own memory is theirs alone.
/// </summary>
public class UserMemoryTests
{
    private const string Operator = "operator-pw";
    private const string Password = "a-long-enough-password";

    [Fact]
    public void The_shared_process_is_served_as_markdown_to_everyone_alike()
    {
        var router = Router();
        var anonymous = router.Route("GET", "/api/process");
        var signedIn = router.Route("GET", "/api/process", SignIn(router, "ann"));

        Assert.Equal(200, anonymous.Status);
        Assert.StartsWith("text/markdown", anonymous.ContentType, StringComparison.Ordinal);
        Assert.Equal(anonymous.Raw, signedIn.Raw);

        var text = System.Text.Encoding.UTF8.GetString(anonymous.Raw!);
        Assert.Contains("# The Kit dev process", text, StringComparison.Ordinal);
        Assert.Contains(".kit/memory.md", text, StringComparison.Ordinal);
        Assert.Contains("/api/me/memory", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_users_memory_round_trips_and_is_empty_before_it_is_written()
    {
        var router = Router();
        var ann = SignIn(router, "ann");

        Assert.Equal(string.Empty, Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", ann).Body).Body);

        var written = router.Route("POST", "/api/me/memory", ann, body: Json(new { body = "prefers small PRs" }));
        Assert.Equal(200, written.Status);
        Assert.Equal("prefers small PRs", Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", ann).Body).Body);

        // Writing replaces: one body per user.
        router.Route("POST", "/api/me/memory", ann, body: Json(new { body = "second" }));
        Assert.Equal("second", Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", ann).Body).Body);
    }

    [Fact]
    public void One_user_never_reads_or_overwrites_anothers_memory()
    {
        var router = Router();
        var ann = SignIn(router, "ann");
        var bob = SignIn(router, "bob");

        router.Route("POST", "/api/me/memory", ann, body: Json(new { body = "ann's secret preference" }));

        // Bob gets his own, empty; a user id in the request is not read at all.
        Assert.Equal(string.Empty, Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", bob).Body).Body);
        router.Route("POST", "/api/me/memory", bob, body: Json(new { body = "bob's", userId = "ann", user = "ann" }));

        Assert.Equal("ann's secret preference", Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", ann).Body).Body);
        Assert.Equal("bob's", Assert.IsType<UserMemory>(router.Route("GET", "/api/me/memory", bob).Body).Body);
    }

    [Fact]
    public void Memory_is_refused_without_a_session()
    {
        var router = Router();
        var store = new MemoryUserMemoryStore();

        Assert.Equal(401, router.Route("GET", "/api/me/memory").Status);
        Assert.Equal(401, router.Route("POST", "/api/me/memory", body: Json(new { body = "x" })).Status);
        Assert.Equal(401, Router(store).Route("POST", "/api/me/memory", "kit_session=forged", body: Json(new { body = "x" })).Status);
        Assert.Null(store.Read(ISessionStore.Operator));
    }

    [Fact]
    public void An_oversized_or_malformed_body_is_refused_and_stores_nothing()
    {
        var store = new MemoryUserMemoryStore();
        var router = Router(store);
        var ann = SignIn(router, "ann");

        Assert.Equal(413, router.Route("POST", "/api/me/memory", ann, body: Json(new { body = new string('x', KitRouter.MaxMemoryBytes + 1) })).Status);
        // The cap is in bytes, not characters.
        Assert.Equal(413, router.Route("POST", "/api/me/memory", ann, body: Json(new { body = new string('é', KitRouter.MaxMemoryBytes / 2 + 1) })).Status);
        Assert.Equal(400, router.Route("POST", "/api/me/memory", ann, body: Json(new { body = 3 })).Status);
        Assert.Equal(400, router.Route("POST", "/api/me/memory", ann, body: Json(new { })).Status);
        Assert.Null(store.Read("ann"));

        Assert.Equal(200, router.Route("POST", "/api/me/memory", ann, body: Json(new { body = new string('x', KitRouter.MaxMemoryBytes) })).Status);
    }

    [Fact]
    public void The_postgres_store_keeps_one_body_per_user()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresUserMemoryStore(connection);
        var a = $"t{Guid.NewGuid():N}"[..20];
        var b = $"u{Guid.NewGuid():N}"[..20];

        Assert.Null(store.Read(a));
        store.Write(a, "one");
        store.Write(a, "two");
        Assert.Equal("two", store.Read(a));
        Assert.Null(store.Read(b));

        // An id is a parameter, never SQL.
        Assert.Null(store.Read("x' OR '1'='1"));
    }

    private static string SignIn(KitRouter router, string name)
    {
        var r = router.Route("POST", "/api/session", body: Json(new { username = name, password = Password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static KitRouter Router(IUserMemoryStore? memory = null)
    {
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(Password, iterations: 1000));
        users.Add("bob", PasswordHash.Hash(Password, iterations: 1000));
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        return new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            Operator,
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle(),
            users: users,
            userMemory: memory);
    }
}
