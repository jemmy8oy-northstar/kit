using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The loopback rule, held directly. The Node tests that held it (<c>isLoopback</c> and the
/// server's default bind in <c>ui.js</c>) went with the Node server; the auth golden still scores
/// the refusal end to end, but nothing there names the default or the lookalike hosts.
/// </summary>
public class LoopbackTests
{
    /// <summary>BEH-UI-2: with nothing configured the host is loopback; only <c>KIT_HOST</c> moves it.</summary>
    [Fact]
    public void The_default_host_is_loopback_not_every_interface()
    {
        var none = KitSettings.FromEnvironment(_ => null, RepoLayout.Root);
        Assert.Equal("127.0.0.1", none.Host);
        Assert.True(OriginPolicy.IsLoopback(none.Host));

        var empty = KitSettings.FromEnvironment(k => k == "KIT_HOST" ? "" : null, RepoLayout.Root);
        Assert.Equal("127.0.0.1", empty.Host);

        var wide = KitSettings.FromEnvironment(k => k == "KIT_HOST" ? "0.0.0.0" : null, RepoLayout.Root);
        Assert.Equal("0.0.0.0", wide.Host);
        Assert.False(OriginPolicy.IsLoopback(wide.Host));
    }

    /// <summary>BEH-WRITE-5: the loopback range and nothing that merely looks like it.</summary>
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.1.2.3", true)]
    [InlineData("localhost", true)]
    [InlineData("::1", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.0.0.5", false)]
    [InlineData("127.0.0.1.evil.com", false)]
    [InlineData("x127.0.0.1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("1270.0.1", false)]
    public void IsLoopback_accepts_the_loopback_range_and_nothing_that_merely_looks_like_it(string host, bool expected) =>
        Assert.Equal(expected, OriginPolicy.IsLoopback(host));
}
