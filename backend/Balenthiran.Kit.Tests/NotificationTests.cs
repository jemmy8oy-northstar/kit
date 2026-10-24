using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Balenthiran.Kit.Abstractions.Exceptions;
using Balenthiran.Kit.Abstractions.Services;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-NOTIFY-1</c>: the agent's messages, read from the feed file it appends to in
/// its own repository (claude-code-bot#118), newest first. Who may read them is pinned in
/// <see cref="AccountsTests"/>; this pins what is read, and what an unreadable feed says.
/// </summary>
public class NotificationTests
{
    private static readonly ProjectSource Feed = ProjectSource.Parse("jemmy8oy-northstar/claude-code-bot@agent/notifications:notifications.jsonl")!;

    [Fact]
    public void Lines_read_newest_first_as_when_kind_and_text()
    {
        var feed = NotificationReader.Parse("""
            {"at":"2026-10-24T14:00:00Z","kind":"wake","text":"waking"}
            {"at":"2026-10-24T14:05:00Z","kind":"pr","text":"raised kit#438"}

            """);

        Assert.Equal(["raised kit#438", "waking"], feed.Entries.Select(e => e.Text));
        Assert.Equal(["pr", "wake"], feed.Entries.Select(e => e.Kind));
        Assert.Equal("2026-10-24T14:05:00Z", feed.Entries[0].At);
        Assert.Equal(0, feed.Skipped);
        Assert.Null(feed.Reason);
    }

    [Fact]
    public void A_line_that_is_not_a_notification_is_skipped_and_counted_never_fatal()
    {
        // One torn write must not hide every message after it.
        var feed = NotificationReader.Parse("""
            {"at":"2026-10-24T14:00:00Z","kind":"wake","text":"first"}
            {"at":"2026-10-24T14:01:00Z","kind":"wa
            not json at all
            {"kind":"note","text":"no time"}
            {"at":"2026-10-24T14:02:00Z","text":42}
            ["an","array"]
            {"at":"2026-10-24T14:03:00Z","kind":"done","text":"last"}
            """);

        Assert.Equal(["last", "first"], feed.Entries.Select(e => e.Text));
        Assert.Equal(5, feed.Skipped);
    }

    [Fact]
    public void An_unknown_or_missing_kind_reads_as_note()
    {
        var feed = NotificationReader.Parse("""
            {"at":"2026-10-24T14:00:00Z","text":"no kind"}
            {"at":"2026-10-24T14:01:00Z","kind":"<script>","text":"odd kind"}
            """);

        Assert.All(feed.Entries, e => Assert.Equal("note", e.Kind));
    }

    [Fact]
    public void The_feed_holds_the_newest_fifty()
    {
        var lines = Enumerable.Range(0, 80).Select(i => $$"""{"at":"2026-10-24T14:00:00Z","kind":"note","text":"n{{i}}"}""");

        var feed = NotificationReader.Parse(string.Join('\n', lines));

        Assert.Equal(NotificationReader.Cap, feed.Entries.Count);
        Assert.Equal("n79", feed.Entries[0].Text);
        Assert.Equal("n30", feed.Entries[^1].Text);
    }

    [Fact]
    public async Task It_reads_the_one_file_raw_with_the_token_and_asks_again_with_its_etag()
    {
        var gh = new FakeGitHub(
            Raw("""{"at":"2026-10-24T14:00:00Z","kind":"wake","text":"waking"}""", "\"e1\""),
            _ => new HttpResponseMessage(HttpStatusCode.NotModified));
        var reader = new NotificationReader(new GitHubCorpusReader(new HttpClient(gh), new StaticGitHubTokenSource("tok")), Feed);

        await reader.RefreshAsync();
        await reader.RefreshAsync();

        Assert.Equal(
            "https://api.github.com/repos/jemmy8oy-northstar/claude-code-bot/contents/notifications.jsonl?ref=agent%2Fnotifications",
            gh.Sent[0].Uri.ToString());
        Assert.All(gh.Sent, s => Assert.Equal("Bearer tok", s.Authorization));

        // The 304 keeps what the first read found.
        Assert.Equal(2, gh.Sent.Count);
        Assert.Equal("waking", Assert.Single(reader.Recent().Entries).Text);
        Assert.Null(reader.Recent().Reason);
    }

    [Fact]
    public async Task A_feed_kit_cannot_read_says_why_and_is_never_shown_as_empty()
    {
        var gh = new FakeGitHub(
            Raw("""{"at":"2026-10-24T14:00:00Z","kind":"wake","text":"waking"}"""),
            FakeGitHub.Json(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));
        var reader = new NotificationReader(new GitHubCorpusReader(new HttpClient(gh), new StaticGitHubTokenSource("tok")), Feed);

        await reader.RefreshAsync();
        await reader.RefreshAsync();

        // A stale list under a fresh-looking page would be the same lie as an empty one.
        var feed = reader.Recent();
        Assert.Empty(feed.Entries);
        Assert.Contains("answered 404", feed.Reason, StringComparison.Ordinal);
        Assert.Contains("Kit's GitHub App may not have access", feed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_feed_configured_the_page_says_so()
    {
        var feed = new NotificationReader(null, null).Recent();

        Assert.Empty(feed.Entries);
        Assert.Contains("KIT_AGENT_FEED is unset", feed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Before_the_first_read_lands_the_feed_is_not_shown_as_empty()
    {
        var reader = new NotificationReader(new FailingFiles(), Feed);
        Assert.Contains("not been read yet", reader.Recent().Reason, StringComparison.Ordinal);

        await reader.RefreshAsync();
        Assert.Equal("GitHub could not be reached", reader.Recent().Reason);
    }

    // BEH-NOTIFY-5: when the agent next wakes, from the pause file its gate reads.
    private static readonly ProjectSource Schedule = ProjectSource.Parse("jemmy8oy-northstar/claude-code-bot@dev:state/suspend.json")!;

    [Fact]
    public void The_agents_own_sleep_says_until_when_and_that_his_action_wakes_it()
    {
        var s = NotificationReader.ParseSchedule("""{"suspended": true, "until": "2026-10-25T07:00:00Z", "reason": "idle overnight", "set_by": "bot"}""");

        Assert.True(s.Suspended);
        Assert.Equal("2026-10-25T07:00:00Z", s.Until);
        Assert.True(s.ByAgent);
        Assert.Equal("idle overnight", s.Why);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void A_pause_he_set_is_not_cut_short_by_kit_and_may_have_no_end()
    {
        var s = NotificationReader.ParseSchedule("""{"suspended": true}""");

        Assert.True(s.Suspended);
        Assert.Null(s.Until);
        Assert.False(s.ByAgent);
    }

    [Theory]
    [InlineData("""{"suspended": false, "until": "2026-10-25T07:00:00Z", "set_by": "bot"}""")]
    [InlineData("""{"until": "2026-10-25T07:00:00Z"}""")]
    [InlineData("""{"suspended": true, "until": "tomorrow-ish", "set_by": "bot"}""")]
    public void It_reads_as_not_paused_exactly_when_the_gate_would_run_a_session(string file)
    {
        var s = NotificationReader.ParseSchedule(file);

        Assert.False(s.Suspended);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void The_gate_reads_a_string_true_as_paused_and_so_does_kit()
    {
        Assert.True(NotificationReader.ParseSchedule("""{"suspended": "true", "until": "2026-10-25T07:00:00Z"}""").Suspended);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[true]")]
    public void A_schedule_that_is_not_an_object_says_so_rather_than_reading_as_awake(string file)
    {
        var s = NotificationReader.ParseSchedule(file);

        Assert.False(s.Suspended);
        Assert.NotNull(s.Reason);
    }

    [Fact]
    public async Task It_reads_the_schedule_with_the_feed_and_an_unreadable_one_says_why()
    {
        var gh = new FakeGitHub(
            Raw("""{"suspended": true, "until": "2026-10-25T07:00:00Z", "set_by": "bot"}"""),
            Raw("""{"at":"2026-10-24T14:00:00Z","kind":"sleep","text":"sleeping"}"""),
            FakeGitHub.Json(HttpStatusCode.NotFound, """{"message":"Not Found"}"""),
            Raw("""{"at":"2026-10-24T14:00:00Z","kind":"sleep","text":"sleeping"}"""));
        var reader = new NotificationReader(new GitHubCorpusReader(new HttpClient(gh), new StaticGitHubTokenSource("tok")), Feed, Schedule);
        Assert.Contains("not been read yet", reader.Recent().Schedule!.Reason, StringComparison.Ordinal);

        await reader.RefreshAsync();
        Assert.Equal(
            "https://api.github.com/repos/jemmy8oy-northstar/claude-code-bot/contents/state/suspend.json?ref=dev",
            gh.Sent[0].Uri.ToString());
        Assert.Equal("2026-10-25T07:00:00Z", reader.Recent().Schedule!.Until);
        Assert.Equal("sleeping", Assert.Single(reader.Recent().Entries).Text);

        // The schedule failing costs the feed nothing, and is never shown as an agent that is awake.
        await reader.RefreshAsync();
        var schedule = reader.Recent().Schedule!;
        Assert.False(schedule.Suspended);
        Assert.Contains("answered 404", schedule.Reason, StringComparison.Ordinal);
        Assert.Single(reader.Recent().Entries);
    }

    [Fact]
    public void With_no_schedule_configured_the_page_says_so()
    {
        Assert.Contains("KIT_AGENT_SCHEDULE is unset", new NotificationReader(null, Feed).Recent().Schedule!.Reason, StringComparison.Ordinal);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Raw(string text, string? etag = null) => r =>
    {
        Assert.Equal("application/vnd.github.raw+json", r.Headers.Accept.Single().MediaType);
        var answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(text)) };
        if (etag is not null)
        {
            answer.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return answer;
    };

    private sealed class FailingFiles : IGitHubFileReader
    {
        public Task<(string Text, string? ETag)?> ReadFileAsync(Abstractions.DataModels.IProjectSource file, string? etag = null, CancellationToken cancellationToken = default) =>
            throw new GitHubReadException("GitHub could not be reached");
    }
}
