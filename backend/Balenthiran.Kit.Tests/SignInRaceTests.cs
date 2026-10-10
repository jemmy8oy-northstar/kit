using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// A blind security review of the C# write gates (2026-10-09): the throttle's check and its
/// count were separate steps, so wrong guesses sent AT ONCE were all checked before any was
/// counted — 400 parallel guesses had 69 evaluated against 5 free attempts. <c>auth.js</c> never
/// had this, because Node runs one request at a time; the port made sign-in concurrent.
/// </summary>
public class SignInRaceTests
{
    [Fact]
    public async Task Guesses_sent_at_once_are_throttled_exactly_as_guesses_sent_in_turn()
    {
        var corpora = new CorpusDirectory(RepoLayout.FrozenBehaviours, RepoLayout.FrozenRoot);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var router = new KitRouter(
            corpora,
            viewer,
            new UiBundle(Path.Combine(RepoLayout.Root, "no-bundle-here")),
            "the-real-password",
            new OriginPolicy(new UrlParser(), null),
            new SessionStore(),
            new SignInThrottle());
        var wrong = JsonDocument.Parse("""{"password":"a guess"}""").RootElement;

        const int n = 200;
        using var start = new Barrier(n);
        var answers = await Task.WhenAll(Enumerable.Range(0, n).Select(_ => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait();
                return router.Route("POST", "/api/session", body: wrong).Status;
            },
            TaskCreationOptions.LongRunning)));

        // FreeAttempts are judged; every other one is refused unread.
        Assert.Equal(SignInThrottle.FreeAttempts, answers.Count(s => s == 401));
        Assert.Equal(n - SignInThrottle.FreeAttempts, answers.Count(s => s == 429));
    }
}
