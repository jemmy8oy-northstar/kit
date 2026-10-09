using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#165: Kestrel serves requests in parallel, where <c>ui.js</c> never could, so the C# server
/// is the first Kit in which two edits can run at once. Each one reads the corpus, writes it,
/// then commits and pushes — two at once raced on the text and on git's index lock. Driven
/// through the real router against a real clone and bare remote, because the race is in how
/// the pieces meet, not in any one of them.
/// </summary>
public class ConcurrentWriteTests
{
    [Fact]
    public async Task Edits_made_at_the_same_moment_all_land_and_all_reach_the_remote()
    {
        var root = Directory.CreateTempSubdirectory("kit-race-").FullName;
        try
        {
            var bare = Path.Combine(root, "remote.git");
            var clone = Path.Combine(root, "clone");
            GitStore.Git(["init", "-q", "--bare", "-b", "main", bare], root);
            GitStore.Git(["clone", "-q", bare, clone], root);
            var dir = Path.Combine(clone, "behaviours");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "demo.beh"), "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
            GitStore.Git(["add", "-A"], clone);
            GitStore.Git(["-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "seed"], clone);
            GitStore.Git(["push", "-q", "origin", "HEAD:main"], clone);

            var corpora = new CorpusDirectory(dir, clone);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            var router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(root, "no-bundle")), null, git: new GitStore(enabled: true, branch: "main"));

            const int n = 8;
            var answers = await Task.WhenAll(Enumerable.Range(1, n).Select(i => Task.Run(() =>
                router.Route("POST", "/api/projects/demo/behaviours/BEH-1/steps", body: JsonDocument.Parse($$"""{"step":"then sees field:F{{i}}"}""").RootElement))));

            Assert.All(answers, a => Assert.Equal(200, a.Status));
            var text = File.ReadAllText(Path.Combine(dir, "demo.beh"));
            for (var i = 1; i <= n; i++)
            {
                Assert.Contains($"then sees field:F{i}\n", text);
            }

            // Every one of them committed and pushed: the remote holds the seed plus n edits.
            var log = GitStore.Git(["--git-dir", bare, "rev-list", "--count", "main"], root);
            Assert.Equal((n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), log.Stdout.Trim());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
