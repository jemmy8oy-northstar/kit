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
        new(new HttpClient(gh), token, repo);

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
}
