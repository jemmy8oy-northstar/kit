using System.Text.Json;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// BEH-SCHED-1..3 (kit#118): an ordered <c>&lt;app&gt;.schedule</c> beside the corpus, apart from the
/// backlog. Scheduling, unscheduling and reordering write only that file — the corpus never changes.
/// </summary>
public class ScheduleTests
{
    private const string Corpus =
        "behaviour BEH-A \"built\"\n  source defined\n  when opens page:Home\n\n"
        + "behaviour BEH-B \"bee\"\n  pending\n  source defined\n  when opens page:Home\n\n"
        + "behaviour BEH-C \"sea\"\n  pending\n  source defined\n  when opens page:Home\n\n"
        + "behaviour BEH-D \"dee\"\n  pending\n  source defined\n  when opens page:Home\n";

    // ---- the file ---------------------------------------------------------------------------

    [Fact]
    public void No_file_is_nothing_scheduled_and_comments_blanks_and_repeats_are_not_ids()
    {
        Assert.Empty(Schedule.Parse(null));
        Assert.Equal(["BEH-B", "BEH-C"], Schedule.Parse("﻿# a comment\r\n\n  BEH-B  \nBEH-C\r\nBEH-B\n"));
    }

    /// <summary>A hand-written reason after the id is not part of it, or the entry would read as missing and its behaviour as backlog.</summary>
    [Fact]
    public void An_id_is_the_first_word_of_its_line()
    {
        Assert.Equal(["BEH-B", "BEH-C"], Schedule.Parse("BEH-B  # he asked for this first\nBEH-C\tlater\n"));
    }

    [Fact]
    public void A_rendered_schedule_parses_back_to_the_same_ids()
    {
        var text = Schedule.Render("demo", ["BEH-C", "BEH-B"]);

        Assert.StartsWith("# ", text);
        Assert.EndsWith("BEH-C\nBEH-B\n", text);
        Assert.Equal(["BEH-C", "BEH-B"], Schedule.Parse(text));
    }

    [Theory]
    [InlineData("add", "BEH-D", "BEH-B,BEH-C,BEH-D")]
    [InlineData("remove", "BEH-B", "BEH-C")]
    [InlineData("up", "BEH-C", "BEH-C,BEH-B")]
    [InlineData("down", "BEH-B", "BEH-C,BEH-B")]
    public void A_change_appends_removes_or_swaps_neighbours(string action, string id, string expected)
    {
        var (ids, error, _) = Schedule.Change(["BEH-B", "BEH-C"], action, id);

        Assert.Null(error);
        Assert.Equal(expected.Split(','), ids);
    }

    [Theory]
    [InlineData("add", "BEH-B", "already-scheduled")]
    [InlineData("remove", "BEH-D", "not-scheduled")]
    [InlineData("up", "BEH-D", "not-scheduled")]
    [InlineData("up", "BEH-B", "cannot-move")]
    [InlineData("down", "BEH-C", "cannot-move")]
    public void A_change_that_cannot_be_made_says_why_and_returns_no_list(string action, string id, string expected)
    {
        var (ids, error, reason) = Schedule.Change(["BEH-B", "BEH-C"], action, id);

        Assert.Null(ids);
        Assert.Equal(expected, error);
        Assert.Contains(id, reason);
    }

    // ---- the routes -------------------------------------------------------------------------

    private sealed class Rig : IDisposable
    {
        public readonly string Root = Directory.CreateTempSubdirectory("kit-schedule-").FullName;
        public KitRouter Router;

        public Rig(string corpus = Corpus)
        {
            File.WriteAllText(Beh, corpus);
            var corpora = new CorpusDirectory(Root, Root);
            var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
            Router = new KitRouter(corpora, viewer, new UiBundle(Path.Combine(Root, "no-bundle")), null);
        }

        public string Beh => Path.Combine(Root, "demo.beh");

        public string ScheduleFile => Path.Combine(Root, "demo.schedule");

        public Balenthiran.Kit.Abstractions.DataModels.IKitResponse Post(string action, string id) =>
            Post(JsonSerializer.Serialize(new { action, id }));

        public Balenthiran.Kit.Abstractions.DataModels.IKitResponse Post(string body) =>
            Router.Route("POST", "/api/projects/demo/schedule", body: JsonDocument.Parse(body).RootElement);

        public JsonElement Get() => JsonSerializer.SerializeToElement(Router.Route("GET", "/api/projects/demo/schedule").Body, Web);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static List<string> Ids(JsonElement list, string which) =>
        list.GetProperty(which).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();

    private static string? NextOf(JsonElement list) =>
        list.GetProperty("scheduled").EnumerateArray().Where(e => e.GetProperty("next").GetBoolean()).Select(e => e.GetProperty("id").GetString()).SingleOrDefault();

    /// <summary>BEH-SCHED-1, BEH-SCHED-3: with nothing scheduled every pending behaviour is backlog and nothing is next.</summary>
    [Fact]
    public void With_nothing_scheduled_every_pending_behaviour_is_backlog_and_nothing_is_next()
    {
        using var rig = new Rig();

        var list = rig.Get();

        Assert.Equal("demo", list.GetProperty("app").GetString());
        Assert.Equal("demo.schedule", list.GetProperty("file").GetString());
        Assert.Empty(Ids(list, "scheduled"));
        Assert.Equal(["BEH-B", "BEH-C", "BEH-D"], Ids(list, "backlog"));
        Assert.Null(NextOf(list));
    }

    /// <summary>BEH-SCHED-2: scheduling writes the schedule beside the corpus, which stays byte-identical.</summary>
    [Fact]
    public void Scheduling_appends_leaves_the_corpus_alone_and_takes_it_out_of_the_backlog()
    {
        using var rig = new Rig();

        var r = rig.Post("add", "BEH-C");
        rig.Post("add", "BEH-B");

        Assert.Equal(200, r.Status);
        Assert.Equal("demo.schedule", JsonSerializer.SerializeToElement(r.Body, Web).GetProperty("file").GetString());
        Assert.Equal(Corpus, File.ReadAllText(rig.Beh));
        Assert.Equal(Schedule.Render("demo", ["BEH-C", "BEH-B"]), File.ReadAllText(rig.ScheduleFile));
        var list = rig.Get();
        Assert.Equal(["BEH-C", "BEH-B"], Ids(list, "scheduled"));
        Assert.Equal(["BEH-D"], Ids(list, "backlog"));
        Assert.Equal("sea", list.GetProperty("scheduled")[0].GetProperty("title").GetString());
    }

    /// <summary>BEH-SCHED-3: next is the top of the schedule.</summary>
    [Fact]
    public void Next_is_the_first_scheduled_behaviour_and_moves_with_it()
    {
        using var rig = new Rig();
        rig.Post("add", "BEH-C");
        rig.Post("add", "BEH-B");
        Assert.Equal("BEH-C", NextOf(rig.Get()));

        Assert.Equal(200, rig.Post("up", "BEH-B").Status);

        Assert.Equal("BEH-B", NextOf(rig.Get()));
        Assert.Equal(200, rig.Post("down", "BEH-B").Status);
        Assert.Equal("BEH-C", NextOf(rig.Get()));
    }

    /// <summary>BEH-SCHED-2: unscheduling returns a behaviour to the backlog; nothing is deleted.</summary>
    [Fact]
    public void Removing_returns_it_to_the_backlog_and_the_corpus_keeps_it()
    {
        using var rig = new Rig();
        rig.Post("add", "BEH-C");

        Assert.Equal(200, rig.Post("remove", "BEH-C").Status);

        var list = rig.Get();
        Assert.Empty(Ids(list, "scheduled"));
        Assert.Equal(["BEH-B", "BEH-C", "BEH-D"], Ids(list, "backlog"));
        Assert.Equal(Corpus, File.ReadAllText(rig.Beh));
    }

    /// <summary>
    /// BEH-SCHED-1: an id that stopped being pending is shown as done, and one the corpus lost as
    /// missing — never dropped silently. Neither is next; the first still-pending one is.
    /// </summary>
    [Fact]
    public void A_built_or_vanished_id_is_listed_as_such_and_next_skips_it()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.ScheduleFile, "BEH-A\nBEH-GONE\nBEH-D\n");

        var list = rig.Get();

        var states = list.GetProperty("scheduled").EnumerateArray().Select(e => (e.GetProperty("id").GetString(), e.GetProperty("state").GetString())).ToList();
        Assert.Equal([("BEH-A", "done"), ("BEH-GONE", "missing"), ("BEH-D", "pending")], states);
        Assert.Equal(JsonValueKind.Null, list.GetProperty("scheduled")[1].GetProperty("title").ValueKind);
        Assert.Equal("BEH-D", NextOf(list));
        Assert.Equal(["BEH-B", "BEH-C"], Ids(list, "backlog"));
    }

    [Fact]
    public void A_schedule_with_nothing_pending_left_has_no_next()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.ScheduleFile, "BEH-A\n");

        Assert.Null(NextOf(rig.Get()));
    }

    /// <summary>A stale entry can always be cleared, even though it could never be added.</summary>
    [Fact]
    public void A_done_or_missing_entry_can_still_be_removed()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.ScheduleFile, "BEH-A\nBEH-GONE\n");

        Assert.Equal(200, rig.Post("remove", "BEH-A").Status);
        Assert.Equal(200, rig.Post("remove", "BEH-GONE").Status);

        Assert.Empty(Ids(rig.Get(), "scheduled"));
    }

    [Theory]
    [InlineData("add", "BEH-A", "not-pending")]
    [InlineData("add", "BEH-NOPE", "no-such-behaviour")]
    [InlineData("remove", "BEH-B", "not-scheduled")]
    [InlineData("up", "BEH-C", "cannot-move")]
    [InlineData("add", "BEH-C", "already-scheduled")]
    public void A_change_that_cannot_be_made_is_a_409_and_writes_nothing(string action, string id, string error)
    {
        using var rig = new Rig();
        rig.Post("add", "BEH-C");
        var before = File.ReadAllText(rig.ScheduleFile);

        var r = rig.Post(action, id);

        Assert.Equal(409, r.Status);
        Assert.Equal(error, JsonSerializer.SerializeToElement(r.Body, Web).GetProperty("error").GetString());
        Assert.Equal(before, File.ReadAllText(rig.ScheduleFile));
    }

    [Theory]
    [InlineData("""{"id":"BEH-B"}""")]
    [InlineData("""{"action":"add"}""")]
    [InlineData("""{"action":"top","id":"BEH-B"}""")]
    [InlineData("""{"action":"add","id":7}""")]
    [InlineData("""{"action":"add","id":"BEH-B\nBEH-C"}""")]
    [InlineData("""{"action":"add","id":"# BEH-B"}""")]
    [InlineData("""[]""")]
    public void A_malformed_change_is_a_400_and_writes_nothing(string body)
    {
        using var rig = new Rig();

        Assert.Equal(400, rig.Post(body).Status);
        Assert.False(File.Exists(rig.ScheduleFile));
    }

    [Fact]
    public void A_schedule_for_a_project_that_does_not_exist_is_a_404_both_ways()
    {
        using var rig = new Rig();

        Assert.Equal(404, rig.Router.Route("POST", "/api/projects/ghost/schedule", body: JsonDocument.Parse("""{"action":"add","id":"BEH-B"}""").RootElement).Status);
        Assert.Equal(404, rig.Router.Route("GET", "/api/projects/ghost/schedule").Status);
    }
}
