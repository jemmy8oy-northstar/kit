using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-hosted.beh <c>BEH-GATE-H1</c>: a read is served to anyone, and locking Kit does not change
/// that. Nothing asserted it before — every locked test was about a write, and every read test
/// ran on a server with no password, where an open read proves nothing about the lock.
/// </summary>
public class ReadGateTests
{
    [Fact]
    public void Locked_a_write_needs_a_session_and_a_read_still_does_not()
    {
        var router = Router(password: "pw");

        // The lock is genuinely on: without this the reads below would pass on an unlocked server.
        Assert.Equal(401, router.Route("POST", "/api/commit").Status);

        Assert.Equal(200, router.Route("GET", "/api/projects").Status);
        Assert.Equal(200, router.Route("GET", "/api/projects/kit").Status);
    }

    private static KitRouter Router(string? password)
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
            new SignInThrottle());
    }
}
