using System.Text;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The write route accepts any non-empty object as a binding, so <c>{"route":1}</c> is written.
/// The generator then throws reading it — as kit.js does — but ui.js catches every throw from
/// <c>project()</c> as could-not-look, and the C# viewer caught only its own exception type: one
/// signed-in write made <c>GET /api/projects</c> a 500 for every reader, every corpus, until the
/// bindings file was hand-edited.
/// </summary>
public class RouteShapeTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("{\"a\":\"/\"}")]
    [InlineData("[\"/\"]")]
    public void A_route_that_is_not_a_string_fails_its_project_and_not_the_list(string route)
    {
        var (host, dir) = Make();
        try
        {
            var body = "{\"noun\":\"page:Home\",\"binding\":{\"route\":" + route + "}}";
            Assert.Equal(200, host.Received("/api/projects/demo/bindings", Encoding.UTF8.GetBytes(body), null, null).Status);

            var one = host.Answer("GET", "/api/projects/demo", null, null);
            Assert.Equal(500, one.Status);
            Assert.Contains("projection-failed", one.Body);
            Assert.Contains("not a string", one.Body);

            var list = host.Answer("GET", "/api/projects", null, null);
            Assert.Equal(200, list.Status);
            Assert.Contains("\"app\":\"other\"", list.Body);
            Assert.Contains("not a string", list.Body);

            Assert.Equal(200, host.Answer("GET", "/api/projects/other", null, null).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The same failure through a second door: the request body is UTF-8, but a JSON escape
    /// spells a lone surrogate, which the write keeps and System.Text.Json cannot read back.
    /// </summary>
    [Fact]
    public void A_lone_surrogate_written_into_a_binding_fails_its_project_and_not_the_list()
    {
        var (host, dir) = Make();
        try
        {
            var body = "{\"noun\":\"page:Home\",\"binding\":{\"route\":\"/\\ud800\"}}";
            Assert.Equal(200, host.Received("/api/projects/demo/bindings", Encoding.UTF8.GetBytes(body), null, null).Status);

            Assert.Equal(500, host.Answer("GET", "/api/projects/demo", null, null).Status);
            var list = host.Answer("GET", "/api/projects", null, null);
            Assert.Equal(200, list.Status);
            Assert.Contains("\"app\":\"other\"", list.Body);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The write route refuses to write any of these, but a bindings file also arrives by git —
    /// a hand edit or a bad merge on the branch a hosted Kit clones. ui.js reads it inside
    /// project()'s catch-all, so only that project fails.
    /// </summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{")]
    // ⚠️ Two cases Node READS — JSON.parse keeps the last duplicate and V8 has no depth limit —
    // and System.Text.Json refuses. Failing the one project is the floor, not parity.
    [InlineData("{\"page:Home\":{\"route\":\"/\"},\"page:Home\":{\"route\":\"/b\"}}")]
    [InlineData("{\"page:Home\":{\"route\":\"/\",\"x\":[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]}}")]
    public void A_bindings_file_that_will_not_read_fails_its_project_and_not_the_list(string content)
    {
        var (host, dir) = Make();
        try
        {
            File.WriteAllText(Path.Combine(dir, "demo.bindings.json"), content);

            var one = host.Answer("GET", "/api/projects/demo", null, null);
            Assert.Equal(500, one.Status);
            Assert.Contains("projection-failed", one.Body);

            var list = host.Answer("GET", "/api/projects", null, null);
            Assert.Equal(200, list.Status);
            Assert.Contains("\"app\":\"other\"", list.Body);
            Assert.Equal(200, host.Answer("GET", "/api/projects/other", null, null).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A corpus a git pull removes between the listing and the read. The viewed project fails
    /// without naming a server path (the list is unauthenticated), and every OTHER project's
    /// view skips it in its cross-corpus noun scan rather than failing too.
    /// </summary>
    [Fact]
    public void A_corpus_that_vanishes_fails_itself_without_a_path_and_not_the_others()
    {
        var (_, dir) = Make();
        try
        {
            var corpora = new VanishingCorpus(new Balenthiran.Kit.Database.CorpusDirectory(dir, RepoLayout.Root), "demo");
            var host = Host(corpora, dir);

            var one = host.Answer("GET", "/api/projects/demo", null, null);
            Assert.Equal(500, one.Status);
            Assert.Contains("could not be read", one.Body);
            Assert.DoesNotContain("Could not find file", one.Body);

            var list = host.Answer("GET", "/api/projects", null, null);
            Assert.Equal(200, list.Status);
            Assert.Contains("could not be read", list.Body);
            Assert.DoesNotContain("Could not find file", list.Body);
            Assert.Equal(200, host.Answer("GET", "/api/projects/other", null, null).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static (KitHost Host, string Dir) Make()
    {
        var dir = Directory.CreateTempSubdirectory("kit-route-").FullName;
        File.WriteAllText(Path.Combine(dir, "demo.beh"), "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
        File.WriteAllText(Path.Combine(dir, "other.beh"), "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
        return (Host(new Balenthiran.Kit.Database.CorpusDirectory(dir, RepoLayout.Root), dir), dir);
    }

    private static KitHost Host(Abstractions.Services.ICorpusDirectory corpora, string dir)
    {
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var policy = new OriginPolicy(new UrlParser(), null);
        return new KitHost(new KitRouter(corpora, viewer, new UiBundle(dir), null, policy), new UrlParser(), Serialiser, policy, string.Empty);
    }

    /// <summary>Lists <paramref name="gone"/> but throws reading it, as File.ReadAllText does — path in the message.</summary>
    private sealed class VanishingCorpus(Abstractions.Services.ICorpusDirectory inner, string gone) : Abstractions.Services.ICorpusDirectory
    {
        public IReadOnlyList<string> Corpora() => inner.Corpora();

        public string Read(string app) => app == gone
            ? throw new FileNotFoundException($"Could not find file '{inner.FullPath(app)}'.")
            : inner.Read(app);

        public System.Text.Json.Nodes.JsonObject Bindings(string app) => inner.Bindings(app);

        public string RelativePath(string app) => inner.RelativePath(app);

        public string RelativeBindingsPath(string app) => inner.RelativeBindingsPath(app);

        public string FullPath(string app) => inner.FullPath(app);

        public string FullBindingsPath(string app) => inner.FullBindingsPath(app);

        public string ReadText(string app) => inner.ReadText(app);

        public string? ReadBindingsText(string app) => inner.ReadBindingsText(app);

        public void WriteText(string app, string text) => inner.WriteText(app, text);

        public void WriteBindingsText(string app, string text) => inner.WriteBindingsText(app, text);

        public string? ReadNotesText(string app) => inner.ReadNotesText(app);

        public void WriteNotesText(string app, string text) => inner.WriteNotesText(app, text);

        public string RelativeNotesPath(string app) => inner.RelativeNotesPath(app);

        public string FullNotesPath(string app) => inner.FullNotesPath(app);
    }
}
