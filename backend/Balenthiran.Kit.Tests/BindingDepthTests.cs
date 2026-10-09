using System.Text;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// A binding is written through <see cref="JsValue.FromElement"/>, which recurses, and read
/// back by System.Text.Json, which stops at 64 levels. V8 has neither limit, so ui.js's
/// golden never needed a bound: here a binding ~60 deep was accepted and then made every GET
/// of the project throw, and one ~17,000 deep overflowed the stack and killed the server.
/// </summary>
public class BindingDepthTests
{
    private static readonly EngineJsonSerialiser Serialiser = new();

    [Fact]
    public void The_deepest_allowed_binding_writes_and_the_project_still_reads()
    {
        var (host, dir) = Make();
        try
        {
            var bind = Bind(host, CorpusWriter.MaxBindingDepth);
            Assert.Equal(200, bind.Status);

            // The two reads that used to fail: the project, and the 409 that echoes the binding.
            Assert.Equal(200, host.Answer("GET", "/api/projects/demo", null, null).Status);
            Assert.Equal(409, Bind(host, 1).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(CorpusWriter.MaxBindingDepth + 1)]
    [InlineData(60)]
    [InlineData(30_000)] // deep enough to overflow FromElement's recursion
    public void A_deeper_binding_is_refused_and_nothing_is_written(int depth)
    {
        var (host, dir) = Make();
        try
        {
            var a = Bind(host, depth);
            Assert.Equal(409, a.Status);
            Assert.Contains("bad-binding", a.Body);
            Assert.False(File.Exists(Path.Combine(dir, "demo.bindings.json")));
            Assert.Equal(200, host.Answer("GET", "/api/projects/demo", null, null).Status);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// A binding of total nesting <paramref name="depth"/>: the deep value sits behind a shallow
    /// first key, so a depth count that only followed the first branch would pass it.
    /// </summary>
    private static Abstractions.DataModels.IHostAnswer Bind(KitHost host, int depth)
    {
        var inner = new string('[', depth - 1) + new string(']', depth - 1);
        var body = "{\"noun\":\"page:Home\",\"binding\":{\"role\":\"button\",\"a\":" + (depth == 1 ? "1" : inner) + "}}";
        return host.Received("/api/projects/demo/bindings", Encoding.UTF8.GetBytes(body), null, null);
    }

    private static (KitHost Host, string Dir) Make()
    {
        var dir = Directory.CreateTempSubdirectory("kit-depth-").FullName;
        File.WriteAllText(Path.Combine(dir, "demo.beh"), "behaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
        var corpora = new Balenthiran.Kit.Database.CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var policy = new OriginPolicy(new UrlParser(), null);
        return (new KitHost(new KitRouter(corpora, viewer, new UiBundle(dir), null, policy), new UrlParser(), Serialiser, policy, string.Empty), dir);
    }
}
