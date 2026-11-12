using System.Net;
using System.Text.Json;
using Balenthiran.Kit.Database;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#147: the Commit button's door to <c>dev</c>. Against a scripted GitHub, because the
/// properties that matter are about which requests are sent — a second press must not open
/// a second pull request, and a refusal must not leak the token into the UI.
/// </summary>
public class GitHubPullRequestOpenerTests
{
    private const string Token = "ghp_SECRET_never_shown";
    private const string Repo = "jemmy8oy-northstar/kit";

    private static GitHubPullRequestOpener Opener(FakeGitHub gh, string? token = Token, string? repo = Repo) =>
        new(new HttpClient(gh), new StaticGitHubTokenSource(token), repo);

    [Fact]
    public async Task One_already_open_is_returned_and_no_second_one_is_created()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """[{"number":12,"html_url":"https://github.com/jemmy8oy-northstar/kit/pull/12"}]"""));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.True(r.AlreadyOpen);
        Assert.False(r.Opened);
        Assert.Equal(12, r.Number);
        Assert.Equal("https://github.com/jemmy8oy-northstar/kit/pull/12", r.Url);
        Assert.Null(r.Reason);
        var only = Assert.Single(gh.Sent);
        Assert.Equal(HttpMethod.Get, only.Method);
        // owner:branch, or GitHub matches nothing and every press opens a duplicate.
        Assert.Equal(
            "https://api.github.com/repos/jemmy8oy-northstar/kit/pulls?state=open&head=jemmy8oy-northstar%3Akit%2Fhosted&base=dev",
            only.Uri.AbsoluteUri);
        Assert.Equal($"Bearer {Token}", only.Authorization);
    }

    /// <summary>BEH-USERS-12: a project's own repository, when named, is the one asked; its owner is the head's owner too.</summary>
    [Fact]
    public async Task A_named_repository_is_asked_instead_of_the_clones()
    {
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, "[]"),
            FakeGitHub.Json(HttpStatusCode.Created, """{"number":4,"html_url":"https://github.com/someone/beta/pull/4"}"""),
            FakeGitHub.Json(HttpStatusCode.OK, """{"commits":[]}"""));

        Assert.Equal(4, (await Opener(gh).OpenAsync("kit/edits", "dev", "t", "b", "someone/beta")).Number);
        await Opener(gh).PendingAsync("kit/edits", "dev", "someone/beta");

        Assert.Equal("https://api.github.com/repos/someone/beta/pulls?state=open&head=someone%3Akit%2Fedits&base=dev", gh.Sent[0].Uri.AbsoluteUri);
        Assert.Equal("https://api.github.com/repos/someone/beta/compare/dev...kit/edits?per_page=100", gh.Sent[^1].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task None_open_creates_one_from_the_head_into_the_base()
    {
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, "[]"),
            FakeGitHub.Json(HttpStatusCode.Created, """{"number":13,"html_url":"https://github.com/jemmy8oy-northstar/kit/pull/13"}"""));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "kit: 2 edits", "from the phone");

        Assert.True(r.Opened);
        Assert.False(r.AlreadyOpen);
        Assert.Equal(13, r.Number);
        Assert.Equal("https://github.com/jemmy8oy-northstar/kit/pull/13", r.Url);
        Assert.Equal(2, gh.Sent.Count);
        var post = gh.Sent[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("https://api.github.com/repos/jemmy8oy-northstar/kit/pulls", post.Uri.AbsoluteUri);
        using var sent = JsonDocument.Parse(post.Body!);
        Assert.Equal("kit/hosted", sent.RootElement.GetProperty("head").GetString());
        Assert.Equal("dev", sent.RootElement.GetProperty("base").GetString());
        Assert.Equal("kit: 2 edits", sent.RootElement.GetProperty("title").GetString());
        Assert.Equal("from the phone", sent.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Nothing_to_propose_says_so_in_GitHubs_words()
    {
        // GitHub's top-level message is "Validation Failed"; the sentence a reader needs is under errors.
        var gh = new FakeGitHub(
            FakeGitHub.Json(HttpStatusCode.OK, "[]"),
            FakeGitHub.Json(HttpStatusCode.UnprocessableEntity, """{"message":"Validation Failed","errors":[{"resource":"PullRequest","code":"custom","message":"No commits between dev and kit/hosted"}]}"""));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.False(r.Opened);
        Assert.False(r.AlreadyOpen);
        Assert.Null(r.Number);
        Assert.Equal("GitHub answered 422: Validation Failed — No commits between dev and kit/hosted", r.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""", "GitHub answered 401: Bad credentials")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Resource not accessible by personal access token"}""", "GitHub answered 403: Resource not accessible by personal access token")]
    [InlineData(HttpStatusCode.NotFound, "", "GitHub answered 404: NotFound")]
    public async Task A_refusal_names_the_status_and_never_the_token(HttpStatusCode status, string json, string reason)
    {
        var gh = new FakeGitHub(FakeGitHub.Json(status, json));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.Equal(reason, r.Reason);
        Assert.DoesNotContain(Token, r.Reason);
        Assert.Single(gh.Sent);
    }

    [Fact]
    public async Task An_unreachable_GitHub_is_a_different_sentence_from_a_refusal()
    {
        var gh = new FakeGitHub(_ => throw new HttpRequestException("Name or service not known (api.github.com:443)"));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.Equal("GitHub could not be reached (Name or service not known (api.github.com:443))", r.Reason);
    }

    [Fact]
    public async Task A_success_with_an_unreadable_body_is_not_reported_as_success()
    {
        var gh = new FakeGitHub(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>proxy</html>") });

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.False(r.Opened);
        Assert.False(r.AlreadyOpen);
        Assert.Equal("GitHub answered 200 with a body Kit could not read", r.Reason);
    }

    /// <summary>
    /// Found by a blind review of kit#160: a 2xx whose JSON is the wrong SHAPE reached
    /// <c>TryGetProperty</c> on a non-object and threw, so Commit answered an unhandled 500
    /// where every other outcome is a sentence.
    /// </summary>
    [Theory]
    [InlineData("[]", "null")]
    [InlineData("[]", "[1]")]
    [InlineData("[]", """{"number":"13","html_url":7}""")]
    [InlineData("[1]", null)]
    [InlineData("""[{"number":12,"html_url":null}]""", null)]
    public async Task A_success_of_the_wrong_shape_is_a_reason_not_a_throw(string list, string? created)
    {
        var gh = created is null
            ? new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, list))
            : new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, list), FakeGitHub.Json(HttpStatusCode.Created, created));

        var r = await Opener(gh).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.False(r.Opened);
        Assert.False(r.AlreadyOpen);
        Assert.StartsWith("GitHub answered 20", r.Reason);
        Assert.EndsWith("that is not a pull request", r.Reason);
    }

    /// <summary>
    /// Also from that review: a token pasted with its trailing newline (a secret created from a
    /// file) must not turn into a throw outside the opener's catch.
    /// </summary>
    [Fact]
    public async Task A_token_with_a_trailing_newline_is_sent_trimmed()
    {
        var gh = new FakeGitHub(FakeGitHub.Json(HttpStatusCode.OK, """[{"number":12,"html_url":"https://github.com/o/k/pull/12"}]"""));

        var r = await Opener(gh, token: Token + "\n").OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.True(r.AlreadyOpen);
        Assert.Equal($"Bearer {Token}", Assert.Single(gh.Sent).Authorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_token_sends_nothing(string? token)
    {
        var gh = new FakeGitHub();

        var r = await Opener(gh, token: token).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.Equal("no GitHub token is set (KIT_GIT_TOKEN), so Kit cannot open a pull request", r.Reason);
        Assert.Empty(gh.Sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("kit")]
    [InlineData("/kit")]
    [InlineData("owner/")]
    public async Task No_repository_sends_nothing(string? repo)
    {
        var gh = new FakeGitHub();

        var r = await Opener(gh, repo: repo).OpenAsync("kit/hosted", "dev", "t", "b");

        Assert.Equal("Kit does not know which GitHub repository its clone came from, so it cannot open a pull request", r.Reason);
        Assert.Empty(gh.Sent);
    }

    [Theory]
    [InlineData("https://github.com/jemmy8oy-northstar/kit", "jemmy8oy-northstar/kit")]
    [InlineData("https://github.com/jemmy8oy-northstar/kit.git", "jemmy8oy-northstar/kit")]
    [InlineData("https://github.com/jemmy8oy-northstar/kit/", "jemmy8oy-northstar/kit")]
    [InlineData("https://x-access-token@github.com/jemmy8oy-northstar/kit.git", "jemmy8oy-northstar/kit")]
    [InlineData("git@github.com:jemmy8oy-northstar/balenthiran.co.uk.git", "jemmy8oy-northstar/balenthiran.co.uk")]
    [InlineData("ssh://git@github.com/jemmy8oy-northstar/kit.git", "jemmy8oy-northstar/kit")]
    [InlineData("  https://github.com/jemmy8oy-northstar/kit\n", "jemmy8oy-northstar/kit")]
    [InlineData("https://gitlab.com/jemmy8oy-northstar/kit.git", null)]
    [InlineData("https://github.com.evil.example/jemmy8oy-northstar/kit", null)]
    [InlineData("/tmp/bare.git", null)]
    [InlineData("https://github.com/jemmy8oy-northstar", null)]
    [InlineData(null, null)]
    public void The_repository_is_read_from_a_GitHub_remote_and_nothing_else(string? url, string? expected)
    {
        Assert.Equal(expected, GitHubPullRequestOpener.RepositoryFromRemote(url));
    }

    // ---- BEH-ORCH-10: merging a built pull request once its checks pass ------------------------------

    private const string Page = "https://github.com/someone/demo/pull/7";
    private const string Head = "abc123def456";

    private static Func<HttpRequestMessage, HttpResponseMessage> Pull(string mergeable = "true", string state = "open") =>
        FakeGitHub.Json(HttpStatusCode.OK, $$$"""{"state":"{{{state}}}","mergeable":{{{mergeable}}},"head":{"sha":"{{{Head}}}"}}""");

    private static Func<HttpRequestMessage, HttpResponseMessage> Runs(params string[] runs) =>
        FakeGitHub.Json(HttpStatusCode.OK, $$"""{"total_count":{{runs.Length}},"workflow_runs":[{{string.Join(',', runs)}}]}""");

    private static string Run(string status, string? conclusion, string name = "ci") =>
        $$"""{"name":"{{name}}","status":"{{status}}","conclusion":{{(conclusion is null ? "null" : $"\"{conclusion}\"")}}}""";

    [Fact]
    public async Task BEH_ORCH_10_merges_the_exact_head_commit_when_every_run_succeeded_and_it_is_mergeable()
    {
        var gh = new FakeGitHub(Pull(), Runs(Run("completed", "success"), Run("completed", "success", "e2e")), FakeGitHub.Json(HttpStatusCode.OK, """{"merged":true}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.True(r.Merged);
        Assert.Null(r.Reason);
        Assert.Equal("https://api.github.com/repos/someone/demo/pulls/7", gh.Sent[0].Uri.AbsoluteUri);
        Assert.Equal($"https://api.github.com/repos/someone/demo/actions/runs?head_sha={Head}&per_page=100", gh.Sent[1].Uri.AbsoluteUri);
        var put = gh.Sent[2];
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("https://api.github.com/repos/someone/demo/pulls/7/merge", put.Uri.AbsoluteUri);
        using var sent = JsonDocument.Parse(put.Body!);
        Assert.Equal(Head, sent.RootElement.GetProperty("sha").GetString());
        Assert.Equal("merge", sent.RootElement.GetProperty("merge_method").GetString());
    }

    [Fact]
    public async Task BEH_ORCH_10_waits_while_a_run_is_in_progress_or_there_is_none_yet_or_mergeable_is_null()
    {
        var running = new FakeGitHub(Pull(), Runs(Run("completed", "success"), Run("in_progress", null)));
        var none = new FakeGitHub(Pull(), Runs());
        var unknown = new FakeGitHub(Pull("null"), Runs(Run("completed", "success")));

        foreach (var gh in new[] { running, none, unknown })
        {
            var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);
            Assert.True(r.Waiting);
            Assert.False(r.Merged);
            Assert.DoesNotContain(gh.Sent, s => s.Method == HttpMethod.Put);
        }
    }

    [Fact]
    public async Task BEH_ORCH_10_a_skipped_run_beside_a_success_merges_but_only_skipped_runs_wait()
    {
        // kit's "Claude Code" workflow shows up as skipped: it ran nothing, so it must not block a merge, nor pass one alone.
        var beside = new FakeGitHub(Pull(), Runs(Run("completed", "skipped", "Claude Code"), Run("completed", "success")), FakeGitHub.Json(HttpStatusCode.OK, """{"merged":true}"""));
        var alone = new FakeGitHub(Pull(), Runs(Run("completed", "skipped", "Claude Code"), Run("completed", "neutral")));

        Assert.True((await Opener(beside).MergeIfGreenAsync(Page, null, CancellationToken.None)).Merged);
        var r = await Opener(alone).MergeIfGreenAsync(Page, null, CancellationToken.None);
        Assert.True(r.Waiting);
        Assert.DoesNotContain(alone.Sent, s => s.Method == HttpMethod.Put);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("timed_out")]
    [InlineData("action_required")]
    public async Task BEH_ORCH_10_a_run_that_did_not_finish_cleanly_refuses(string conclusion)
    {
        var gh = new FakeGitHub(Pull(), Runs(Run("completed", "success"), Run("completed", conclusion, "e2e")));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.Equal($"e2e did not succeed ({conclusion})", r.Reason);
        Assert.DoesNotContain(gh.Sent, s => s.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task BEH_ORCH_10_refuses_a_failed_run_even_while_another_is_still_running()
    {
        var gh = new FakeGitHub(Pull(), Runs(Run("in_progress", null), Run("completed", "failure", "unit tests")));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.False(r.Merged);
        Assert.False(r.Waiting);
        Assert.Equal("unit tests did not succeed (failure)", r.Reason);
        Assert.DoesNotContain(gh.Sent, s => s.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task BEH_ORCH_10_refuses_a_conflict_and_a_closed_pull_request_and_merges_neither()
    {
        var conflict = new FakeGitHub(Pull("false"), Runs(Run("completed", "success")));
        var closed = new FakeGitHub(Pull(state: "closed"));

        var c = await Opener(conflict).MergeIfGreenAsync(Page, null, CancellationToken.None);
        var x = await Opener(closed).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.Equal("it has a conflict with its base branch", c.Reason);
        Assert.Equal("its pull request is closed", x.Reason);
        Assert.DoesNotContain(conflict.Sent.Concat(closed.Sent), s => s.Method == HttpMethod.Put);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task BEH_ORCH_10_refuses_when_the_checks_cannot_be_read_saying_so_and_never_the_token(HttpStatusCode status)
    {
        var gh = new FakeGitHub(Pull(), FakeGitHub.Json(status, """{"message":"Resource not accessible by integration"}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.False(r.Merged);
        Assert.False(r.Waiting);
        Assert.StartsWith("Kit cannot read the checks of this pull request", r.Reason);
        Assert.DoesNotContain(Token, r.Reason);
        Assert.DoesNotContain(gh.Sent, s => s.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task BEH_ORCH_10_a_head_that_moved_after_the_check_is_looked_at_again_not_merged()
    {
        var gh = new FakeGitHub(Pull(), Runs(Run("completed", "success")), FakeGitHub.Json(HttpStatusCode.Conflict, """{"message":"Head branch was modified. Review and try the merge again."}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.True(r.Waiting);
        Assert.False(r.Merged);
    }

    [Fact]
    public async Task BEH_ORCH_10_an_address_that_is_not_a_pull_request_page_is_refused_without_a_request()
    {
        var gh = new FakeGitHub();

        var r = await Opener(gh).MergeIfGreenAsync("https://example.com/someone/demo/pull/7", null, CancellationToken.None);

        Assert.NotNull(r.Reason);
        Assert.Empty(gh.Sent);
    }

    // ---- BEH-ORCH-11, slice 7: the merge job brings the branch up to date first ----------------------

    private static Func<HttpRequestMessage, HttpResponseMessage> PullOnDev() =>
        FakeGitHub.Json(HttpStatusCode.OK, $$$"""{"state":"open","mergeable":true,"head":{"sha":"{{{Head}}}"},"base":{"ref":"dev"}}""");

    private static Func<HttpRequestMessage, HttpResponseMessage> Compared(int behindBy) =>
        FakeGitHub.Json(HttpStatusCode.OK, $$"""{"status":"ahead","ahead_by":2,"behind_by":{{behindBy}}}""");

    [Fact]
    public async Task BEH_ORCH_11_a_branch_behind_its_base_is_updated_and_waited_on_before_its_checks_are_read()
    {
        var gh = new FakeGitHub(PullOnDev(), Compared(3), FakeGitHub.Json(HttpStatusCode.Accepted, """{"message":"Updating pull request branch."}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true);

        Assert.True(r.Waiting);
        Assert.False(r.Merged);
        Assert.Equal("brought up to date with dev", r.Reason);
        Assert.Equal($"https://api.github.com/repos/someone/demo/compare/dev...{Head}?per_page=1", gh.Sent[1].Uri.AbsoluteUri);
        var put = gh.Sent[2];
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("https://api.github.com/repos/someone/demo/pulls/7/update-branch", put.Uri.AbsoluteUri);
        using var sent = JsonDocument.Parse(put.Body!);
        Assert.Equal(Head, sent.RootElement.GetProperty("expected_head_sha").GetString());
        Assert.Equal(3, gh.Sent.Count);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_branch_not_behind_goes_on_to_the_checks_and_the_merge_as_before()
    {
        var gh = new FakeGitHub(PullOnDev(), Compared(0), Runs(Run("completed", "success")), FakeGitHub.Json(HttpStatusCode.OK, """{"merged":true}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true);

        Assert.True(r.Merged);
        Assert.DoesNotContain(gh.Sent, s => s.Uri.AbsoluteUri.EndsWith("/update-branch", StringComparison.Ordinal));
        Assert.Contains("/actions/runs?", gh.Sent[2].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task BEH_ORCH_11_a_branch_github_cannot_update_has_a_conflict_and_is_refused()
    {
        var gh = new FakeGitHub(PullOnDev(), Compared(1), FakeGitHub.Json(HttpStatusCode.UnprocessableEntity, """{"message":"merge conflict between base and head"}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true);

        Assert.False(r.Waiting);
        Assert.False(r.Merged);
        Assert.Equal("it has a conflict with its base branch", r.Reason);
        Assert.True(r.Conflict);
    }

    [Fact]
    public async Task BEH_ORCH_11_only_a_conflict_is_marked_as_one_and_no_other_refusal_or_wait_is()
    {
        var mergeable = new FakeGitHub(Pull("false"), Runs(Run("completed", "success")));
        var closed = new FakeGitHub(Pull(state: "closed"));
        var failed = new FakeGitHub(Pull(), Runs(Run("completed", "failure")));
        var waiting = new FakeGitHub(PullOnDev(), Compared(3), FakeGitHub.Json(HttpStatusCode.Accepted, """{"message":"Updating pull request branch."}"""));
        var denied = new FakeGitHub(PullOnDev(), Compared(1), FakeGitHub.Json(HttpStatusCode.Forbidden, """{"message":"forbidden"}"""));

        Assert.True((await Opener(mergeable).MergeIfGreenAsync(Page, null, CancellationToken.None)).Conflict);
        Assert.False((await Opener(closed).MergeIfGreenAsync(Page, null, CancellationToken.None)).Conflict);
        Assert.False((await Opener(failed).MergeIfGreenAsync(Page, null, CancellationToken.None)).Conflict);
        Assert.False((await Opener(waiting).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true)).Conflict);
        Assert.False((await Opener(denied).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true)).Conflict);
    }

    [Fact]
    public async Task BEH_ORCH_11_without_updateFirst_no_comparison_is_made()
    {
        var gh = new FakeGitHub(PullOnDev(), Runs(Run("completed", "success")), FakeGitHub.Json(HttpStatusCode.OK, """{"merged":true}"""));

        var r = await Opener(gh).MergeIfGreenAsync(Page, null, CancellationToken.None);

        Assert.True(r.Merged);
        Assert.DoesNotContain(gh.Sent, s => s.Uri.AbsoluteUri.Contains("/compare/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BEH_ORCH_11_a_comparison_github_cannot_answer_waits_on_a_server_error_and_refuses_on_a_client_error()
    {
        var down = new FakeGitHub(PullOnDev(), FakeGitHub.Json(HttpStatusCode.BadGateway, """{"message":"bad gateway"}"""));
        var denied = new FakeGitHub(PullOnDev(), FakeGitHub.Json(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));

        Assert.True((await Opener(down).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true)).Waiting);
        var r = await Opener(denied).MergeIfGreenAsync(Page, null, CancellationToken.None, updateFirst: true);
        Assert.False(r.Waiting);
        Assert.NotNull(r.Reason);
    }
}
