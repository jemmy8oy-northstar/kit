using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-GATE-H3, from his note of 2026-10-19 19:28Z: "Everytime kit updates, my browser signs
/// out." A deploy is a new process, so "survives a restart" is tested as a second store built
/// from the same password.
/// </summary>
public class SignedSessionStoreTests
{
    private static readonly byte[] Key = SignedSessionStore.KeyFor("the-real-password");

    [Fact]
    public void A_token_minted_before_a_restart_is_valid_after_it()
    {
        var token = new SignedSessionStore(Key).Create();

        Assert.True(new SignedSessionStore(SignedSessionStore.KeyFor("the-real-password")).Valid(token));
    }

    [Fact]
    public void Changing_the_password_signs_every_session_out()
    {
        var token = new SignedSessionStore(Key).Create();

        Assert.False(new SignedSessionStore(SignedSessionStore.KeyFor("a-new-password")).Valid(token));
    }

    [Fact]
    public void A_token_dies_at_its_ttl()
    {
        long t = 1_000;
        var store = new SignedSessionStore(Key, () => t);
        var token = store.Create();

        t += SessionStore.TtlMs - 1;
        Assert.True(store.Valid(token));
        t += 1;
        Assert.False(store.Valid(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tok-1")]
    [InlineData(".")]
    [InlineData("99999999999999.00.")]
    public void Anything_it_did_not_sign_is_refused(string? token) =>
        Assert.False(new SignedSessionStore(Key).Valid(token));

    [Fact]
    public void Extending_the_expiry_breaks_the_signature()
    {
        var token = new SignedSessionStore(Key).Create();
        var parts = token.Split('.');
        var forged = $"{long.Parse(parts[0]) + SessionStore.TtlMs}.{parts[1]}.{parts[2]}";

        Assert.False(new SignedSessionStore(Key).Valid(forged));
    }

    [Fact]
    public void Sign_out_ends_the_session_and_only_that_session()
    {
        var store = new SignedSessionStore(Key);
        var a = store.Create();
        var b = store.Create();

        Assert.True(store.Destroy(a));
        Assert.False(store.Valid(a));
        Assert.False(store.Destroy(a));
        Assert.True(store.Valid(b));
    }

    [Fact]
    public void A_server_with_a_password_wires_the_signed_store()
    {
        var withPassword = new ServiceCollection().AddKitServices(Settings("the-real-password")).BuildServiceProvider();
        var without = new ServiceCollection().AddKitServices(Settings(null)).BuildServiceProvider();

        Assert.IsType<SignedSessionStore>(withPassword.GetRequiredService<ISessionStore>());
        Assert.IsType<SessionStore>(without.GetRequiredService<ISessionStore>());
    }

    private static KitSettings Settings(string? password) =>
        new(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot, password, Path.Combine(RepoLayout.Root, "no-bundle-here"), string.Empty, null, "127.0.0.1");
}
