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

    /// <summary>BEH-SCHED-2: the schedule is read from GitHub the same way, and a change is read back until GitHub has it.</summary>
    [Fact]
    public async Task The_schedule_comes_from_GitHub_and_a_change_is_read_back_until_GitHub_has_it()
    {
        var reader = new Scripted(
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.schedule", "s1", "BEH-1\n")),
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.schedule", "s1", "BEH-1\n")),
            Snap(Kit, ("alpha.beh", "a1", "x"), ("alpha.schedule", "s2", "BEH-2\nBEH-1\n")));
        var d = new GitHubCorpusDirectory(clone, [Kit], reader);
        await d.RefreshAsync();
        Assert.Equal("BEH-1\n", d.ReadScheduleText("alpha"));
        Assert.Equal("prototypes/corpora/alpha.schedule", d.RelativeSchedulePath("alpha"));

        d.WriteScheduleText("alpha", "BEH-1\nBEH-2\n");
        Assert.Equal("BEH-1\nBEH-2\n", File.ReadAllText(Path.Combine(root, "behaviours", "alpha.schedule")));
        await d.RefreshAsync();
        Assert.Equal("BEH-1\nBEH-2\n", d.ReadScheduleText("alpha"));

        await d.RefreshAsync();
        Assert.Equal("BEH-2\nBEH-1\n", d.ReadScheduleText("alpha"));
    }

    [Fact]
    public async Task An_app_from_a_repository_with_no_clone_cannot_be_edited_here()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], new Scripted(Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", "y"))));
        await d.RefreshAsync();

        var e = Assert.Throws<NotWritableException>(() => d.WriteText("beta", "z"));

        Assert.Equal("beta is read from o/snip-it@dev:behaviours, which this Kit has no clone of, so it cannot be edited here", e.Message);
        Assert.False(File.Exists(Path.Combine(root, "behaviours", "beta.beh")));
    }

    /// <summary>The same refusal through the router: a 409 naming the layer, never the 500 it used to be.</summary>
    [Fact]
    public async Task A_write_to_an_app_with_no_clone_is_a_409_not_a_500()
    {
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], new Scripted(Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", "behaviour BEH-B \"b\"\n  when opens page:Home\n"))));
        await d.RefreshAsync();
        var viewer = new Services.ProjectViewer(d, new Services.CorpusParser(), new Services.BehaviourResolver(), new Services.TestGenerator(), new Services.ProjectReporter());
        var router = new Services.KitRouter(d, viewer, new Services.UiBundle(Path.Combine(root, "no-bundle")), null);

        var r = router.Route("POST", "/api/projects/beta/behaviours", body: System.Text.Json.JsonDocument.Parse("""{"id":"BEH-N","title":"new"}""").RootElement);

        Assert.Equal(409, r.Status);
        var body = System.Text.Json.JsonSerializer.SerializeToElement(r.Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("not-writable", body.GetProperty("error").GetString());
        Assert.StartsWith("beta is read from o/snip-it@dev:behaviours", body.GetProperty("reason").GetString());
        Assert.False(File.Exists(Path.Combine(root, "behaviours", "beta.beh")));
    }

    private const string Beta = "behaviour BEH-B \"b\"\n  when opens page:Home\n";

    private async Task<(GitHubCorpusDirectory D, Services.KitRouter Router)> OwnRepo(Writer writer, params ICorpusSnapshot[] more)
    {
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], new Scripted([Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", Beta)), .. more]), writer);
        await d.RefreshAsync();
        var viewer = new Services.ProjectViewer(d, new Services.CorpusParser(), new Services.BehaviourResolver(), new Services.TestGenerator(), new Services.ProjectReporter());
        return (d, new Services.KitRouter(d, viewer, new Services.UiBundle(Path.Combine(root, "no-bundle")), null));
    }

    private static System.Text.Json.JsonElement Body(IKitResponse r) =>
        System.Text.Json.JsonSerializer.SerializeToElement(r.Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    private static IKitResponse AddBehaviour(Services.KitRouter router, string id) =>
        router.Route("POST", "/api/projects/beta/behaviours", body: System.Text.Json.JsonDocument.Parse($$"""{"id":"{{id}}","title":"new"}""").RootElement);

    /// <summary>BEH-USERS-12: a write to an app in its own repository is one commit there, on the branch it is read from, naming the blob Kit read.</summary>
    [Fact]
    public async Task A_write_to_an_app_in_its_own_repo_is_committed_there_and_read_back()
    {
        var writer = new Writer();
        var (d, router) = await OwnRepo(writer);

        var r = AddBehaviour(router, "BEH-N");

        Assert.Equal(200, r.Status);
        var put = Assert.Single(writer.Puts);
        Assert.Equal((Other, "beta.beh", "b1", "kit: add BEH-N (beta)"), (put.Source, put.File, put.Sha, put.Message));
        Assert.Contains("BEH-N", put.Text);
        Assert.Equal(put.Text, d.ReadText("beta"));
        var body = Body(r);
        Assert.Equal("c1", body.GetProperty("commit").GetString());
        Assert.Equal("dev", body.GetProperty("branch").GetString());
        Assert.False(File.Exists(Path.Combine(root, "behaviours", "beta.beh")));
    }

    /// <summary>A second write before GitHub is re-read names the blob the first one committed, or GitHub would refuse Kit's own edit as stale.</summary>
    [Fact]
    public async Task A_second_write_before_a_re_read_names_the_blob_the_first_committed()
    {
        var writer = new Writer();
        var (_, router) = await OwnRepo(writer);

        AddBehaviour(router, "BEH-N");
        AddBehaviour(router, "BEH-M");

        Assert.Equal(["b1", "blob1"], writer.Puts.Select(p => p.Sha));
    }

    /// <summary>Someone else changed the file first: nothing is overwritten, the page stops showing the edit, and Kit re-reads at once.</summary>
    [Fact]
    public async Task A_stale_write_is_a_409_and_the_edit_is_not_kept()
    {
        var writer = new Writer { Refuse = new GitHubWriteException("beta.beh was changed by someone else first", stale: true) };
        var (d, router) = await OwnRepo(writer, Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b2", "theirs")));

        var r = AddBehaviour(router, "BEH-N");

        Assert.Equal(409, r.Status);
        Assert.Equal("beta.beh was changed by someone else first", Body(r).GetProperty("reason").GetString());
        for (var i = 0; i < 100 && d.ReadText("beta") != "theirs"; i++)
        {
            await Task.Delay(10);
        }

        Assert.Equal("theirs", d.ReadText("beta"));
    }

    /// <summary>Any other refusal also keeps nothing: an edit on the page that reached no repository would be a lie.</summary>
    [Fact]
    public async Task A_refused_write_is_a_409_with_GitHubs_reason_and_the_old_text_stays()
    {
        var writer = new Writer { Refuse = new GitHubWriteException("o/snip-it@dev:behaviours: GitHub answered 403 to committing beta.beh: Resource not accessible by integration") };
        var (d, router) = await OwnRepo(writer);

        var r = AddBehaviour(router, "BEH-N");

        Assert.Equal(409, r.Status);
        Assert.EndsWith("Resource not accessible by integration", Body(r).GetProperty("reason").GetString());
        Assert.Equal(Beta, d.ReadText("beta"));
    }

    /// <summary>
    /// The poller swaps in someone else's change between a route's read and its write: the commit names the
    /// blob the text was read from, so GitHub refuses it rather than lose their change. Synchronous on purpose,
    /// as a route is: the read and the write happen on one thread.
    /// </summary>
    [Fact]
    public void A_re_read_between_the_read_and_the_write_cannot_change_the_blob_the_commit_names()
    {
        var writer = new Writer();
        var d = new GitHubCorpusDirectory(clone, [Kit, Other], new Scripted(Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", Beta)), Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b2", "theirs"))), writer);
        d.RefreshAsync().GetAwaiter().GetResult();

        var text = d.ReadText("beta") + "  then sees region:Main\n";
        d.RefreshAsync().GetAwaiter().GetResult();
        d.WriteText("beta", text);
        d.CommitToOwnRepo("beta", "kit: x");

        Assert.Equal("b1", Assert.Single(writer.Puts).Sha);
    }

    /// <summary>A failed commit puts back the edit before it, which is on GitHub, rather than show text older than both.</summary>
    [Fact]
    public async Task A_failed_commit_puts_back_the_committed_edit_before_it()
    {
        var writer = new Writer();
        var (d, router) = await OwnRepo(writer, Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "b1", Beta)));
        AddBehaviour(router, "BEH-N");
        var committed = d.ReadText("beta");
        writer.Refuse = new GitHubWriteException("GitHub answered 502");

        Assert.Equal(409, AddBehaviour(router, "BEH-M").Status);

        Assert.Equal(committed, d.ReadText("beta"));
    }

    /// <summary>An edit whose app a re-read dropped before its commit is refused, not left showing as if saved.</summary>
    [Fact]
    public async Task An_edit_whose_app_left_GitHub_before_its_commit_is_refused_and_not_shown()
    {
        var writer = new Writer();
        var (d, _) = await OwnRepo(writer, Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other));

        d.WriteText("beta", "edited");
        await d.RefreshAsync();

        Assert.Throws<NotWritableException>(() => d.CommitToOwnRepo("beta", "kit: x"));
        Assert.Throws<KeyNotFoundException>(() => d.ReadText("beta"));
        Assert.Empty(writer.Puts);
    }

    /// <summary>The clone's apps are untouched: they are written to the clone and committed by its git write-back, never through the Contents API.</summary>
    [Fact]
    public async Task An_app_the_clone_holds_never_goes_through_the_writer()
    {
        var writer = new Writer();
        var (d, _) = await OwnRepo(writer);

        d.WriteText("alpha", "edited");

        Assert.Null(d.CommitToOwnRepo("alpha", "kit: x"));
        Assert.Empty(writer.Puts);
        Assert.Equal("edited", File.ReadAllText(Path.Combine(root, "behaviours", "alpha.beh")));
    }

    /// <summary>The commit is the re-read's: once GitHub serves the committed blob, the override ends and GitHub answers again.</summary>
    [Fact]
    public async Task Once_GitHub_has_the_commit_GitHub_answers_again()
    {
        var writer = new Writer();
        var (d, router) = await OwnRepo(writer, Snap(Kit, ("alpha.beh", "a1", "x")), Snap(Other, ("beta.beh", "blob1", "as GitHub has it")));
        AddBehaviour(router, "BEH-N");

        await d.RefreshAsync();

        Assert.Equal("as GitHub has it", d.ReadText("beta"));
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

    /// <summary>
    /// BEH-PULL-5 (kit#300) end to end, over HTTP: inside the age a request asks GitHub nothing;
    /// past it, the request itself is what reads, and it is answered from that read. Nothing else
    /// would read: there is no timer, so a server that ignored the age would still list gamma.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("/kit")] // deployed: the ingress does not strip the prefix, so the API is /kit/api/…
    public async Task A_request_past_the_age_reads_GitHub_and_is_answered_from_that_read(string basePath)
    {
        var env = new Dictionary<string, string?>
        {
            ["KIT_DIR"] = Path.Combine(root, "behaviours"),
            ["KIT_PROJECTS"] = "o/kit@kit/hosted:behaviours",
            ["KIT_GIT_TOKEN"] = "tok",
            ["KIT_BASE_PATH"] = basePath,
        };
        var settings = KitSettings.FromEnvironment(k => env.GetValueOrDefault(k), RepoLayout.Root);
        static Func<HttpRequestMessage, HttpResponseMessage> Beh(string id) =>
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes($"behaviour {id} \"from GitHub\"\n  when opens page:Home\n")) };
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, """[{"type": "file", "name": "gamma.beh", "sha": "g1"}]"""),
            Beh("BEH-G"),
            FakeGitHub.Json(HttpStatusCode.OK, """[{"type": "file", "name": "delta.beh", "sha": "d1"}]"""),
            Beh("BEH-D"));
        var clock = new ManualClock();
        await using var app = KitServer.Build(["--urls", "http://127.0.0.1:0"], settings, s =>
        {
            s.AddSingleton<HttpMessageHandler>(gh);
            s.AddSingleton<TimeProvider>(clock);
        });
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        Assert.Contains("\"gamma\"", await http.GetStringAsync($"{basePath}/api/projects"), StringComparison.Ordinal);
        var asked = gh.Sent.Count;
        clock.Now += settings.ProjectsPoll - TimeSpan.FromSeconds(1);
        Assert.Contains("\"gamma\"", await http.GetStringAsync($"{basePath}/api/projects"), StringComparison.Ordinal);
        Assert.Equal(asked, gh.Sent.Count);

        clock.Now += TimeSpan.FromSeconds(1);
        var listed = await http.GetStringAsync($"{basePath}/api/projects");

        Assert.Contains("\"delta\"", listed, StringComparison.Ordinal);
        Assert.DoesNotContain("\"gamma\"", listed, StringComparison.Ordinal);
        await app.StopAsync();
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
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

    /// <summary>Records each commit and answers blob1, blob2… and c1, c2…, or throws <see cref="Refuse"/>.</summary>
    private sealed class Writer : IGitHubCorpusWriter
    {
        public List<(IProjectSource Source, string File, string Text, string? Sha, string Message)> Puts { get; } = [];

        public GitHubWriteException? Refuse { get; set; }

        public Task<(string Blob, string Commit)> PutAsync(IProjectSource source, string fileName, string text, string? sha, string message, CancellationToken cancellationToken = default)
        {
            if (Refuse is not null)
            {
                throw Refuse;
            }

            Puts.Add((source, fileName, text, sha, message));
            return Task.FromResult(($"blob{Puts.Count}", $"c{Puts.Count}"));
        }
    }

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
