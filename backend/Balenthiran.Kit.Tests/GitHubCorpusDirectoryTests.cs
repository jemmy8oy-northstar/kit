using System.Net;
using System.Text;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.WebApi;
using Microsoft.Extensions.DependencyInjection;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// PULL slice 2 (kit#88, BEH-PULL-1): reads from the last GitHub snapshot, writes to the clone.
/// The reader is scripted here — its own requests are <see cref="GitHubCorpusReaderTests"/>'s.
/// </summary>
public sealed class GitHubCorpusDirectoryTests : IDisposable
{
    private static readonly ProjectSource Kit = ProjectSource.Parse("o/kit@kit/hosted:prototypes/corpora")!;
    private static readonly ProjectSource Other = ProjectSource.Parse("o/snip-it@dev:behaviours")!;

    private readonly string root = Directory.CreateTempSubdirectory("kit-pull-").FullName;
    private readonly CorpusDirectory clone;

    public GitHubCorpusDirectoryTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "behaviours"));
        File.WriteAllText(Path.Combine(root, "behaviours", "alpha.beh"), "behaviour BEH-A \"on disk\"\n");
        clone = new CorpusDirectory(Path.Combine(root, "behaviours"), root);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void Before_the_first_refresh_the_clone_answers_rather_than_an_empty_list()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit], new Scripted());

        Assert.False(d.FromGitHub);
        Assert.Equal(["alpha"], d.Corpora());
        Assert.Equal("behaviour BEH-A \"on disk\"\n", d.Read("alpha"));
    }

    [Fact]
    public async Task After_a_refresh_every_read_is_GitHubs_and_the_list_is_GitHubs_alone()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit], new Scripted(Snap(Kit, ("alpha.beh", "a2", "behaviour BEH-A \"on GitHub\"\n"), ("beta.beh", "b1", "\ufeffbehaviour BEH-B \"b\"\n"), ("beta.bindings.json", "j1", "{\"page:Home\": {\"route\": \"./\"}}"))));

        await d.RefreshAsync();

        Assert.True(d.FromGitHub);
        Assert.Equal(["alpha", "beta"], d.Corpora());
        Assert.Equal("behaviour BEH-A \"on GitHub\"\n", d.Read("alpha"));
        Assert.Equal("\ufeffbehaviour BEH-B \"b\"\n", d.ReadText("beta")); // a splice sees the BOM
        Assert.Equal("behaviour BEH-B \"b\"\n", d.Read("beta"));         // a parse does not
        Assert.Equal("./", d.Bindings("beta")["page:Home"]!["route"]!.GetValue<string>());
        Assert.Empty(d.Bindings("alpha"));
        Assert.Null(d.ReadBindingsText("alpha"));
        Assert.Equal("prototypes/corpora/beta.beh", d.RelativePath("beta"));
        Assert.Equal("prototypes/corpora/beta.bindings.json", d.RelativeBindingsPath("beta"));
    }

    [Fact]
    public async Task Each_refresh_hands_the_reader_its_last_snapshot_so_an_unchanged_directory_is_a_304()
    {
        var first = Snap(Kit, ("alpha.beh", "a1", "x"));
        var reader = new Scripted(first, first);
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);

        await d.RefreshAsync();
        await d.RefreshAsync();

        Assert.Equal([null, first], reader.Previous);
    }

    [Fact]
    public async Task Two_sources_naming_one_app_refuse_and_the_last_good_snapshot_stays()
    {
        var reader = new Scripted(Snap(Kit, ("alpha.beh", "a1", "kept")), Snap(Other), Snap(Kit, ("alpha.beh", "a1", "kept")), Snap(Other, ("alpha.beh", "z1", "theirs")));
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], reader);
        await d.RefreshAsync();

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => d.RefreshAsync());

        Assert.Equal("alpha.beh is in both o/kit@kit/hosted:prototypes/corpora and o/snip-it@dev:behaviours; Kit will not choose one", e.Message);
        Assert.Equal("kept", d.Read("alpha"));
    }

    [Fact]
    public async Task A_failed_read_keeps_the_last_snapshot_rather_than_serving_half_of_one()
    {
        var reader = new Scripted(Snap(Kit, ("alpha.beh", "a1", "kept")), Snap(Other, ("beta.beh", "b1", "b")), Snap(Kit, ("alpha.beh", "a2", "newer")));
        reader.FailOn = 4;
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], reader);
        await d.RefreshAsync();

        await Assert.ThrowsAsync<GitHubReadException>(() => d.RefreshAsync());

        Assert.Equal("kept", d.Read("alpha"));
        Assert.Equal(["alpha", "beta"], d.Corpora());
    }

    [Fact]
    public async Task A_write_lands_in_the_clone_and_is_read_back_before_GitHub_has_it()
    {
        var reader = new Scripted(Snap(Kit, ("alpha.beh", "a1", "old")), Snap(Kit, ("alpha.beh", "a1", "old")));
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);
        await d.RefreshAsync();

        d.WriteText("alpha", "edited");

        Assert.Equal("edited", File.ReadAllText(Path.Combine(root, "behaviours", "alpha.beh")));
        Assert.Equal("edited", d.Read("alpha"));

        // A refresh that saw GitHub before the push still has sha a1: it may not roll the edit back.
        await d.RefreshAsync();
        Assert.Equal("edited", d.Read("alpha"));
    }

    [Fact]
    public async Task Once_GitHubs_sha_moves_GitHub_answers_again()
    {
        var reader = new Scripted(Snap(Kit, ("alpha.beh", "a1", "old")), Snap(Kit, ("alpha.beh", "a2", "pushed, then edited on GitHub")));
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);
        await d.RefreshAsync();
        d.WriteText("alpha", "edited");

        await d.RefreshAsync();

        Assert.Equal("pushed, then edited on GitHub", d.Read("alpha"));
    }

    [Fact]
    public async Task A_new_bindings_file_is_read_back_until_GitHub_lists_one()
    {
        var reader = new Scripted(Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.bindings.json", "j1", "{\"page:X\": {}}")));
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);
        await d.RefreshAsync();

        d.WriteBindingsText("alpha", "{\"page:Home\": {}}");
        await d.RefreshAsync();
        Assert.Equal("{\"page:Home\": {}}", d.ReadBindingsText("alpha"));

        await d.RefreshAsync();
        Assert.Equal("{\"page:X\": {}}", d.ReadBindingsText("alpha"));
    }

    /// <summary>kit#118: notes are read from GitHub like the corpus, and a note just left is read back until GitHub has it.</summary>
    [Fact]
    public async Task Notes_come_from_GitHub_and_a_new_note_is_read_back_until_GitHub_has_it()
    {
        var reader = new Scripted(
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.notes.md", "n1", "on GitHub")),
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.notes.md", "n1", "on GitHub")),
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.notes.md", "n2", "pushed")));
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);
        await d.RefreshAsync();
        Assert.Equal("on GitHub", d.ReadNotesText("alpha"));
        Assert.Equal("prototypes/corpora/alpha.notes.md", d.RelativeNotesPath("alpha"));

        d.WriteNotesText("alpha", "left in Kit");
        Assert.Equal("left in Kit", File.ReadAllText(Path.Combine(root, "behaviours", "alpha.notes.md")));
        await d.RefreshAsync();
        Assert.Equal("left in Kit", d.ReadNotesText("alpha"));

        await d.RefreshAsync();
        Assert.Equal("pushed", d.ReadNotesText("alpha"));
    }

    [Fact]
    public async Task An_app_from_a_repository_with_no_clone_cannot_be_edited_here()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], new Scripted(Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", "y"))));
        await d.RefreshAsync();

        var e = Assert.Throws<InvalidOperationException>(() => d.WriteText("beta", "z"));

        Assert.Equal("beta is read from o/snip-it@dev:behaviours, which this Kit has no clone of, so it cannot be edited here", e.Message);
        Assert.False(File.Exists(Path.Combine(root, "behaviours", "beta.beh")));
    }

    [Fact]
    public void The_full_paths_a_commit_needs_are_the_clones()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit], new Scripted());

        Assert.Equal(clone.FullPath("alpha"), d.FullPath("alpha"));
        Assert.Equal(clone.FullBindingsPath("alpha"), d.FullBindingsPath("alpha"));
    }

    // ── settings ──────────────────────────────────────────────────────────────────

    [Fact]
    public void KIT_PROJECTS_is_a_list_and_unset_is_none()
    {
        Assert.Empty(KitSettings.ParseProjects(null));
        Assert.Empty(KitSettings.ParseProjects("  "));
        Assert.Equal(
            ["o/kit@kit/hosted:behaviours", "o/snip-it@dev:x/behaviours"],
            KitSettings.ParseProjects("o/kit@kit/hosted:behaviours,\n o/snip-it@dev:x/behaviours").Select(s => s.ToString()));
    }

    [Theory]
    [InlineData("o/kit@dev:behaviours, nonsense", "KIT_PROJECTS entry \"nonsense\" is not owner/repo@branch:path")]
    [InlineData("o/kit@dev:behaviours o/kit@dev:behaviours", "KIT_PROJECTS names o/kit@dev:behaviours twice")]
    public void A_bad_KIT_PROJECTS_refuses_to_start_rather_than_drop_a_project(string value, string message)
    {
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => KitSettings.ParseProjects(value)).Message);
    }

    [Theory]
    [InlineData(null, 120)]
    [InlineData("30", 30)]
    [InlineData("10", 10)]
    public void KIT_PROJECTS_POLL_is_seconds(string? value, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), KitSettings.ParsePoll(value));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("soon")]
    public void A_poll_that_is_not_whole_seconds_of_ten_or_more_refuses(string value)
    {
        Assert.Throws<InvalidOperationException>(() => KitSettings.ParsePoll(value));
    }

    // ── the registered server ────────────────────────────────────────────────────────

    /// <summary>
    /// The chain from the environment to the page: KIT_PROJECTS set, the registered directory is
    /// GitHub's, it asks GitHub with the token, and the project list a browser gets is GitHub's.
    /// </summary>
    [Fact]
    public async Task The_registered_server_lists_the_projects_GitHub_holds()
    {
        var env = new Dictionary<string, string?>
        {
            ["KIT_DIR"] = Path.Combine(root, "behaviours"),
            ["KIT_PROJECTS"] = "o/kit@kit/hosted:behaviours",
            ["KIT_GIT_TOKEN"] = "tok",
        };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """[{"type": "file", "name": "gamma.beh", "sha": "g1"}]"""),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("behaviour BEH-G \"from GitHub\"\n  when opens page:Home\n")) });
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s => s.AddSingleton<HttpMessageHandler>(gh));
        var host = app.Services.GetRequiredService<IKitHost>();

        Assert.Contains("\"alpha\"", host.Answer("GET", "/api/projects", null, null).Body, StringComparison.Ordinal);

        await app.Services.GetRequiredService<GitHubCorpusDirectory>().RefreshAsync();
        var listed = host.Answer("GET", "/api/projects", null, null).Body;

        Assert.Contains("\"gamma\"", listed, StringComparison.Ordinal);
        Assert.DoesNotContain("\"alpha\"", listed, StringComparison.Ordinal);
        Assert.Equal("Bearer tok", gh.Sent[0].Authorization);
        Assert.Same(app.Services.GetRequiredService<GitHubCorpusDirectory>(), app.Services.GetRequiredService<ICorpusDirectory>());
    }

    [Fact]
    public async Task With_no_KIT_PROJECTS_the_server_reads_the_disk_exactly_as_before()
    {
        var env = new Dictionary<string, string?> { ["KIT_DIR"] = Path.Combine(root, "behaviours") };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings);

        Assert.IsType<CorpusDirectory>(app.Services.GetRequiredService<ICorpusDirectory>());
        Assert.Null(app.Services.GetService<GitHubCorpusDirectory>());
    }

    private static CorpusSnapshot Snap(IProjectSource source, params (string Name, string Sha, string Text)[] files) => new()
    {
        Source = source,
        Files = files.ToDictionary(f => f.Name, f => (ISnapshotFile)new SnapshotFile { Sha = f.Sha, Text = f.Text }, StringComparer.Ordinal),
    };

    /// <summary>Answers each read with the next snapshot, and records the previous snapshot it was handed.</summary>
    private sealed class Scripted(params ICorpusSnapshot[] answers) : IGitHubCorpusReader
    {
        private int next;

        public List<ICorpusSnapshot?> Previous { get; } = [];

        public int FailOn { get; set; }

        public Task<ICorpusSnapshot> ReadAsync(IProjectSource source, ICorpusSnapshot? previous = null, CancellationToken cancellationToken = default)
        {
            Previous.Add(previous);
            if (++next == FailOn)
            {
                throw new GitHubReadException($"{source}: GitHub did not answer in time");
            }

            return Task.FromResult(answers[next - 1]);
        }
    }
}
