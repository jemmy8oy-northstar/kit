using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// The GitHub read path's first slice (kit#88, BEH-PULL-1..3): one source read through the REST
/// API, against a scripted GitHub. Not wired into the server yet — these pin what it SENDS as
/// much as what it returns, because the rate limit is paid in requests.
/// </summary>
public class GitHubCorpusReaderTests
{
    private static readonly ProjectSource Kit = ProjectSource.Parse("jemmy8oy-northstar/kit@kit/hosted:prototypes/behaviour-ast/behaviours")!;

    private const string Listing = """
        [
          {"type": "file", "name": "snip-it.beh", "sha": "s1"},
          {"type": "file", "name": "snip-it.bindings.json", "sha": "b1"},
          {"type": "file", "name": "alpha.beh", "sha": "a1"},
          {"type": "file", "name": "kit.tests.json", "sha": "t1"},
          {"type": "dir", "name": "nested.beh", "sha": "d1"},
          {"type": "file", "name": "README.md", "sha": "r1"}
        ]
        """;

    [Fact]
    public async Task Reads_the_corpus_files_of_one_directory_and_nothing_else()
    {
        var gh = new FakeGitHub(Listed(Listing, "W/\"e1\""), Raw("behaviour BEH-S \"s\"\n"), Raw("{}\n"), Raw("behaviour BEH-A \"a\"\n"));

        var snap = await Reader(gh, "tok").ReadAsync(Kit);

        // Code-unit order, and only .beh / .bindings.json files directly in the directory.
        Assert.Equal(["alpha.beh", "snip-it.beh", "snip-it.bindings.json"], snap.Files.Keys);
        Assert.Equal("behaviour BEH-A \"a\"\n", snap.Files["alpha.beh"].Text);
        Assert.Equal("a1", snap.Files["alpha.beh"].Sha);
        Assert.Equal("W/\"e1\"", snap.ETag);

        Assert.Equal(4, gh.Sent.Count);
        Assert.Equal("https://api.github.com/repos/jemmy8oy-northstar/kit/contents/prototypes/behaviour-ast/behaviours?ref=kit%2Fhosted", gh.Sent[0].Uri.ToString());
        Assert.Equal(
            ["/git/blobs/s1", "/git/blobs/b1", "/git/blobs/a1"],
            gh.Sent.Skip(1).Select(s => s.Uri.AbsolutePath[s.Uri.AbsolutePath.IndexOf("/git/", StringComparison.Ordinal)..]));
        Assert.All(gh.Sent, s => Assert.Equal("Bearer tok", s.Authorization));
    }

    /// <summary>kit#118: a project's open notes travel with it, or a note left in Kit would vanish from Kit once GitHub answers.</summary>
    [Fact]
    public async Task A_notes_file_is_read_with_its_corpus()
    {
        var gh = new FakeGitHub(Listed("""[{"type": "file", "name": "alpha.beh", "sha": "a1"}, {"type": "file", "name": "alpha.notes.md", "sha": "n1"}]"""), Raw("behaviour BEH-A \"a\"\n"), Raw("# Notes on alpha\n"));

        var snap = await Reader(gh, "tok").ReadAsync(Kit);

        Assert.Equal(["alpha.beh", "alpha.notes.md"], snap.Files.Keys);
        Assert.Equal("# Notes on alpha\n", snap.Files["alpha.notes.md"].Text);
    }

    [Fact]
    public async Task A_BOM_survives_so_a_splice_written_back_changes_only_its_line()
    {
        var bom = "\ufeffbehaviour BEH-S \"s\"\n";
        var gh = new FakeGitHub(Listed("""[{"type": "file", "name": "s.beh", "sha": "s1"}]"""), Raw(bom));

        var snap = await Reader(gh, "tok").ReadAsync(Kit);

        Assert.Equal(bom, snap.Files["s.beh"].Text);
    }

    [Fact]
    public async Task An_unchanged_directory_costs_one_conditional_request_and_returns_the_snapshot_it_was_given()
    {
        string? sentEtag = null;
        var first = await Reader(new FakeGitHub(Listed(Listing, "W/\"e1\""), Raw("s"), Raw("b"), Raw("a")), "tok").ReadAsync(Kit);
        var gh = new FakeGitHub(r =>
        {
            sentEtag = r.Headers.IfNoneMatch.ToString();
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });

        var again = await Reader(gh, "tok").ReadAsync(Kit, first);

        Assert.Same(first, again);
        Assert.Single(gh.Sent);
        Assert.Equal("W/\"e1\"", sentEtag);
    }

    [Fact]
    public async Task A_changed_directory_fetches_only_the_files_whose_sha_moved()
    {
        var first = await Reader(new FakeGitHub(Listed(Listing, "W/\"e1\""), Raw("s"), Raw("b"), Raw("a")), "tok").ReadAsync(Kit);
        var moved = Listing.Replace("\"sha\": \"s1\"", "\"sha\": \"s2\"", StringComparison.Ordinal);
        var gh = new FakeGitHub(Listed(moved, "W/\"e2\""), Raw("s, edited"));

        var snap = await Reader(gh, "tok").ReadAsync(Kit, first);

        Assert.Equal(2, gh.Sent.Count);
        Assert.EndsWith("/git/blobs/s2", gh.Sent[1].Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("s, edited", snap.Files["snip-it.beh"].Text);
        Assert.Same(first.Files["alpha.beh"], snap.Files["alpha.beh"]);
        Assert.Equal("W/\"e2\"", snap.ETag);
    }

    [Fact]
    public async Task A_file_deleted_on_GitHub_leaves_the_snapshot()
    {
        var first = await Reader(new FakeGitHub(Listed(Listing, "W/\"e1\""), Raw("s"), Raw("b"), Raw("a")), "tok").ReadAsync(Kit);
        var gh = new FakeGitHub(Listed("""[{"type": "file", "name": "alpha.beh", "sha": "a1"}]""", "W/\"e2\""));

        var snap = await Reader(gh, "tok").ReadAsync(Kit, first);

        Assert.Equal(["alpha.beh"], snap.Files.Keys);
    }

    [Fact]
    public async Task A_snapshot_of_another_source_is_not_offered_as_this_ones_ETag()
    {
        var other = ProjectSource.Parse("jemmy8oy-northstar/snip-it@dev:behaviours")!;
        var first = await Reader(new FakeGitHub(Listed("[]", "W/\"e1\"")), "tok").ReadAsync(other);
        string? sentEtag = "unset";
        var gh = new FakeGitHub(r =>
        {
            sentEtag = r.Headers.IfNoneMatch.ToString();
            return Listed("[]")(r);
        });

        await Reader(gh, "tok").ReadAsync(Kit, first);

        Assert.Equal(string.Empty, sentEtag);
    }

    [Fact]
    public async Task With_no_token_it_reads_without_one_as_a_public_repository_allows()
    {
        var gh = new FakeGitHub(Listed("[]"));

        await Reader(gh, null).ReadAsync(Kit);

        Assert.Null(gh.Sent[0].Authorization);
    }

    [Fact]
    public async Task A_404_without_a_token_says_it_may_be_private_rather_than_absent()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.NotFound, """{"message": "Not Found"}"""));

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(gh, null).ReadAsync(Kit));

        Assert.Equal(
            "jemmy8oy-northstar/kit@kit/hosted:prototypes/behaviour-ast/behaviours: GitHub answered 404: Not Found — the repository, branch or path does not exist, or it is private and no GitHub token is set",
            e.Message);
    }

    [Fact]
    public async Task A_path_that_is_a_file_is_a_configuration_error_not_an_empty_project_list()
    {
        var gh = new FakeGitHub(Listed("""{"type": "file", "name": "behaviours", "sha": "x"}"""));

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(gh, "tok").ReadAsync(Kit));

        Assert.Contains("did not answer a directory listing", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_listed_corpus_with_no_sha_is_refused_rather_than_skipped()
    {
        var gh = new FakeGitHub(Listed("""[{"type": "file", "name": "a.beh"}]"""));

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(gh, "tok").ReadAsync(Kit));

        Assert.Contains("listed a.beh with no sha", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_blob_fails_the_read_so_no_project_silently_disappears()
    {
        var gh = new FakeGitHub(Listed(Listing), Raw("s"), FakeGitHub.Json(HttpStatusCode.Forbidden, """{"message": "API rate limit exceeded"}"""));

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(gh, "tok").ReadAsync(Kit));

        Assert.EndsWith("GitHub answered 403: API rate limit exceeded", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_GitHub_and_a_failed_mint_name_their_layer_and_never_the_token()
    {
        var down = new FakeGitHub(_ => throw new HttpRequestException("Name or service not known"));
        var e1 = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(down, "tok-secret").ReadAsync(Kit));
        Assert.EndsWith("GitHub could not be reached (Name or service not known)", e1.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-secret", e1.Message, StringComparison.Ordinal);

        var e2 = await Assert.ThrowsAsync<GitHubReadException>(() => new GitHubCorpusReader(new HttpClient(new FakeGitHub()), new FailingTokens()).ReadAsync(Kit));
        Assert.EndsWith(": the App would not mint", e2.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("jemmy8oy-northstar/kit@kit/hosted:prototypes/behaviour-ast/behaviours", "jemmy8oy-northstar", "kit", "kit/hosted", "prototypes/behaviour-ast/behaviours")]
    [InlineData(" o/r@dev:/behaviours/ ", "o", "r", "dev", "behaviours")]
    public void A_source_is_read_from_its_one_spelling(string text, string owner, string repo, string branch, string path)
    {
        var s = ProjectSource.Parse(text)!;

        Assert.Equal((owner, repo, branch, path), (s.Owner, s.Repository, s.Branch, s.Path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("o/r:behaviours")] // no branch: no default, or it reads the wrong one
    [InlineData("o/r@dev")]
    [InlineData("r@dev:behaviours")]
    [InlineData("o/r@dev:../secrets")]
    [InlineData("o/r@dev:a/./b")]
    [InlineData("o/r@dev:/")]
    [InlineData("o/r@dev:has space")]
    public void Anything_else_is_not_a_source(string? text)
    {
        Assert.Null(ProjectSource.Parse(text));
    }

    private static GitHubCorpusReader Reader(FakeGitHub gh, string? token) =>
        new(new HttpClient(gh), new StaticGitHubTokenSource(token));

    private static Func<HttpRequestMessage, HttpResponseMessage> Listed(string json, string? etag = null) => _ =>
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (etag is not null)
        {
            r.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return r;
    };

    private static Func<HttpRequestMessage, HttpResponseMessage> Raw(string text) => r =>
    {
        // The raw media type is what makes GitHub send the bytes rather than base64 JSON.
        Assert.Equal("application/vnd.github.raw+json", r.Headers.Accept.Single().MediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(text)) };
    };

    private sealed class FailingTokens : IGitHubTokenSource
    {
        public Task<string?> TokenAsync(CancellationToken cancellationToken = default) =>
            throw new GitHubTokenException("the App would not mint");
    }
}
