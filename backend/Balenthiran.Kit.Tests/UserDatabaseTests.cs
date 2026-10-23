using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-USERS-1's accounts in the database Kit runs: <c>KIT_DATABASE</c> switches them on, and the
/// store is scored against a real Postgres when <c>KIT_TEST_DATABASE</c> names one.
/// </summary>
public class UserDatabaseTests
{
    private const string Connection = "Host=db.invalid;Database=kit;Username=kit;Password=the-db-secret";

    [Fact]
    public void KIT_DATABASE_switches_accounts_on_and_never_prints()
    {
        var settings = KitSettings.FromEnvironment(Env(("KIT_DATABASE", Connection), ("KIT_PASSWORD", "pw")), RepoLayout.Root);

        Assert.True(settings.Accounts);
        Assert.Equal(KitRouter.DefaultOperator, settings.Operator);
        Assert.DoesNotContain("the-db-secret", settings.ToString(), StringComparison.Ordinal);

        // Wired without connecting: the table is made on first use, so a database that is down
        // costs a sign-in rather than the whole Kit.
        var router = new ServiceCollection().AddKitServices(settings).BuildServiceProvider().GetRequiredService<IKitRouter>();
        Assert.True(Assert.IsType<SessionState>(router.Route("GET", "/api/session").Body).Accounts);
    }

    [Fact]
    public void Without_KIT_DATABASE_there_are_no_accounts()
    {
        var settings = KitSettings.FromEnvironment(Env(("KIT_PASSWORD", "pw")), RepoLayout.Root);
        var services = new ServiceCollection().AddKitServices(settings).BuildServiceProvider();

        Assert.False(settings.Accounts);
        Assert.Null(services.GetService<IUserStore>());
        Assert.Null(Assert.IsType<SessionState>(services.GetRequiredService<IKitRouter>().Route("GET", "/api/session").Body).Accounts);
    }

    [Fact]
    public void KIT_DATABASE_without_a_password_refuses_to_start()
    {
        var e = Assert.Throws<InvalidOperationException>(() => KitSettings.FromEnvironment(Env(("KIT_DATABASE", Connection)), RepoLayout.Root));
        Assert.Contains("KIT_PASSWORD", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KIT_OPERATOR_names_him()
    {
        var settings = KitSettings.FromEnvironment(Env(("KIT_DATABASE", Connection), ("KIT_PASSWORD", "pw"), ("KIT_OPERATOR", "jb")), RepoLayout.Root);
        Assert.Equal("jb", settings.Operator);
    }

    [Fact]
    public void The_store_keeps_adds_and_replaces_hashes_in_postgres()
    {
        // Needs a real database, so CI without one skips it by passing. Run it with KIT_TEST_DATABASE set.
        if (Environment.GetEnvironmentVariable("KIT_TEST_DATABASE") is not { Length: > 0 } connection)
        {
            return;
        }

        using var store = new PostgresUserStore(connection);
        var name = $"t{Guid.NewGuid():N}"[..20];

        Assert.Null(store.HashOf(name));
        Assert.False(store.SetHash(name, "h0"));
        Assert.True(store.Add(name, "h1"));
        Assert.False(store.Add(name, "h2"));
        Assert.Equal("h1", store.HashOf(name));
        Assert.True(store.SetHash(name, "h3"));
        Assert.Equal("h3", store.HashOf(name));

        // A name is a parameter, never SQL.
        Assert.Null(store.HashOf("x' OR '1'='1"));

        // BEH-USERS-1's GitHub: one installation, one user, enforced by the table.
        var other = $"u{Guid.NewGuid():N}"[..20];
        var installation = Random.Shared.NextInt64(1L << 40, 1L << 50);
        Assert.True(store.Add(other, "h"));
        Assert.Null(store.GitHubOf(name));
        Assert.False(store.SetGitHub($"n{Guid.NewGuid():N}"[..20], new GitHubLink { Installation = installation, Account = "x" }));
        Assert.True(store.SetGitHub(name, new GitHubLink { Installation = installation, Account = "ann-gh" }));
        Assert.Equal((installation, "ann-gh"), (store.GitHubOf(name)!.Installation, store.GitHubOf(name)!.Account));
        Assert.False(store.SetGitHub(other, new GitHubLink { Installation = installation, Account = "ann-gh" }));
        Assert.Null(store.GitHubOf(other));
    }

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs)
    {
        var env = pairs.ToDictionary(p => p.Key, p => p.Value);
        return k => env.GetValueOrDefault(k);
    }
}
