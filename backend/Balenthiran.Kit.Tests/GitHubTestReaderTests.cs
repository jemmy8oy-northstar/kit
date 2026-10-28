using System.Net;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-HEALTH-1's reader: a repo's test titles through GitHub's REST API, against a scripted GitHub.
/// Like the corpus reader these pin what is SENT, because the rate limit is paid in requests: one
/// recursive tree, then a blob only for a test file whose sha is new to this reader.
/// </summary>
public class GitHubTestReaderTests
{
    private const string Repo = "jemmy8oy-northstar/snip-it@dev";

    // Split so this file's own attribute count (kit check reads it too) sees no test in a string.
    private const string FactLine ="[" + "Fact]";

    private const string Tests = "public class ThingTests\n{\n    " + FactLine + "\n    public void It_works() { }\n}\n";

    private const string MoreTests = "public class ThingTests\n{\n    " + FactLine + "\n    public void It_works() { }\n\n    " + FactLine + "\n    public void It_also_works() { }\n}\n";

    private static string Tree(string thing, string spec = "s1") => $$"""
        {"sha": "t", "truncated": false, "tree": [
          {"path": "README.md", "type": "blob", "sha": "r1"},
          {"path": "backend", "type": "tree", "sha": "d1"},
          {"path": "backend/ThingTests.cs", "type": "blob", "sha": "{{thing}}"},
          {"path": "frontend/e2e/editor.spec.ts", "type": "blob", "sha": "{{spec}}"},
          {"path": "frontend/node_modules/dep/dep.spec.ts", "type": "blob", "sha": "n1"},
          {"path": "backend/bin/Debug/ThingTests.cs", "type": "blob", "sha": "b1"}
        ]}
        """;

    private const string Spec = "test('it edits', async () => {});\n";

    private static GitHubCorpusReader Reader(FakeGitHub gh) => new(new HttpClient(gh), new StaticGitHubTokenSource("tok"));

    private static CoverageCounter Counter() => new(new TestTitleReader());

    private static string Path(Uri uri) => uri.AbsolutePath[uri.AbsolutePath.IndexOf("/git/", StringComparison.Ordinal)..];

    [Fact]
    public async Task Reads_one_recursive_tree_then_only_the_test_files_outside_skipped_directories()
    {
        var gh = new FakeGitHub(GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Tests), GitHubCorpusReaderTests.Raw(Spec));

        var reading = await Reader(gh).ReadTestsAsync(Repo, Counter());

        Assert.NotNull(reading);
        Assert.Null(reading.Refusal);
        Assert.Equal(["backend/ThingTests.cs|It_works", "frontend/e2e/editor.spec.ts|it edits"], reading.Titles.Select(t => $"{t.File}|{t.Raw}"));
        Assert.Equal(3, gh.Sent.Count);
        Assert.Equal("https://api.github.com/repos/jemmy8oy-northstar/snip-it/git/trees/dev?recursive=1", gh.Sent[0].Uri.ToString());
        Assert.Equal(["/git/blobs/a1", "/git/blobs/s1"], gh.Sent.Skip(1).Select(s => Path(s.Uri)));
        Assert.All(gh.Sent, s => Assert.Equal("Bearer tok", s.Authorization));
    }

    [Fact]
    public async Task A_second_read_fetches_only_the_blobs_whose_sha_moved()
    {
        var reader = Reader(new FakeGitHub(
            GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Tests), GitHubCorpusReaderTests.Raw(Spec),
            GitHubCorpusReaderTests.Listed(Tree("a2"), "W/\"e2\""), GitHubCorpusReaderTests.Raw(MoreTests)));
        await reader.ReadTestsAsync(Repo, Counter());

        var again = await reader.ReadTestsAsync(Repo, Counter());

        Assert.NotNull(again);
        Assert.Equal(3, again.Titles.Count);
    }

    [Fact]
    public async Task A_second_read_sends_no_blob_request_when_no_sha_moved()
    {
        string? sentEtag = null;
        var gh = new FakeGitHub(
            GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Tests), GitHubCorpusReaderTests.Raw(Spec),
            r =>
            {
                sentEtag = r.Headers.IfNoneMatch.ToString();
                return GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e2\"")(r);
            });
        var reader = Reader(gh);
        await reader.ReadTestsAsync(Repo, Counter());

        var again = await reader.ReadTestsAsync(Repo, Counter());

        Assert.Equal(4, gh.Sent.Count);
        Assert.Equal("W/\"e1\"", sentEtag);
        Assert.Equal(2, again!.Titles.Count);
    }

    [Fact]
    public async Task An_unchanged_tree_is_one_conditional_request_and_says_nothing_changed()
    {
        var gh = new FakeGitHub(
            GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Tests), GitHubCorpusReaderTests.Raw(Spec),
            _ => new HttpResponseMessage(HttpStatusCode.NotModified));
        var reader = Reader(gh);
        await reader.ReadTestsAsync(Repo, Counter());

        var again = await reader.ReadTestsAsync(Repo, Counter());

        Assert.Null(again);
        Assert.Equal(4, gh.Sent.Count);
    }

    [Fact]
    public async Task A_title_count_mismatch_is_a_refusal_not_a_number()
    {
        // A computed title is skipped by the reader and still counted by the second count.
        var gh = new FakeGitHub(
            GitHubCorpusReaderTests.Listed("""{"truncated": false, "tree": [{"path": "e2e/a.spec.ts", "type": "blob", "sha": "x1"}]}"""),
            GitHubCorpusReaderTests.Raw("test(name, () => {});\n"));

        var reading = await Reader(gh).ReadTestsAsync(Repo, Counter());

        Assert.NotNull(reading!.Refusal);
        Assert.Contains("e2e/a.spec.ts: read 0 test(s) but 1 test declaration(s)", reading.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_truncated_tree_is_a_failure_and_never_an_undercount()
    {
        var gh = new FakeGitHub(GitHubCorpusReaderTests.Listed("""{"truncated": true, "tree": []}"""));

        var e = await Assert.ThrowsAsync<GitHubReadException>(() => Reader(gh).ReadTestsAsync(Repo, Counter()));

        Assert.Contains("truncated", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_blob_leaves_the_tree_unread_and_the_retry_keeps_the_blobs_it_had()
    {
        var gh = new FakeGitHub(
            GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Tests), FakeGitHub.Json(HttpStatusCode.BadGateway, """{"message": "bad gateway"}"""),
            GitHubCorpusReaderTests.Listed(Tree("a1"), "W/\"e1\""), GitHubCorpusReaderTests.Raw(Spec));
        var reader = Reader(gh);
        await Assert.ThrowsAsync<GitHubReadException>(() => reader.ReadTestsAsync(Repo, Counter()));

        var again = await reader.ReadTestsAsync(Repo, Counter());

        // No ETag was kept for the half-read tree, so the retry was not answered 304; it fetched only the blob it lacked.
        Assert.Equal(2, again!.Titles.Count);
        Assert.Equal(5, gh.Sent.Count);
        Assert.EndsWith("/git/blobs/s1", gh.Sent[4].Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("jemmy8oy-northstar/snip-it")]
    [InlineData("snip-it@dev")]
    [InlineData("a@b/c")]
    public async Task Anything_but_owner_repo_at_branch_is_not_a_repo_to_read(string repo)
    {
        await Assert.ThrowsAsync<GitHubReadException>(() => Reader(new FakeGitHub()).ReadTestsAsync(repo, Counter()));
    }
}
