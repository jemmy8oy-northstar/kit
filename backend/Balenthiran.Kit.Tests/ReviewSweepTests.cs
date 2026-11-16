using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-13</c>: the scheduled Opus review, a stage of FEAT-AGENT-PIPELINE on a timer the user sets per
/// project, behind its own switch (<c>KIT_PIPELINE_REVIEW</c>). Driven through the pipeline's own rig, as the runner pool
/// sees it: a run is claimed and reported as a runner pod does. <c>BEH-ORCH-3</c> (the timer starts only the review),
/// <c>BEH-SETTINGS-5</c> and <c>BEH-SETTINGS-6</c> (the idle choice and its ordered list are unchanged by the new row).
/// </summary>
public class ReviewSweepTests
{
    // The review run's model and turn limit come from the same settings the other stages use.
    private const string OpusModel = "claude-opus-5-5";

    // One look of the timer, as PipelineReviewJob makes it, then the run it started, if any.
    private static PoolClaim? Tick(PipelineTests.Rig rig)
    {
        rig.Pipeline!.ReviewTick();
        return rig.Claim();
    }

    // Moves the clock on and looks once at each hour, as the timer does, and counts the review runs started over the span.
    private static List<DateTimeOffset> RunsOver(PipelineTests.Rig rig, PipelineTests.Ticking time, TimeSpan span)
    {
        var starts = new List<DateTimeOffset>();
        var end = time.GetUtcNow() + span;
        while (time.GetUtcNow() < end)
        {
            var claim = Tick(rig);
            if (claim is not null)
            {
                starts.Add(time.GetUtcNow());
                rig.Finish(claim);
            }

            time.Advance(TimeSpan.FromHours(1));
        }

        return starts;
    }

    // The idle choice as Settings reads it (BEH-SETTINGS-5): its first item, and the ordered list.
    private static string IdleOf(PipelineTests.Rig rig) => Assert.IsType<IdleSetting>(rig.Router.Route("GET", "/api/settings/idle").Body).Idle;

    [Fact]
    public void BEH_ORCH_13_With_the_cadence_off_no_review_run_starts_however_long_the_timer_runs()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);

        var starts = RunsOver(rig, time, TimeSpan.FromDays(60));

        Assert.Empty(starts);
        Assert.Null(rig.Claim());
        Assert.Equal(ReviewCadence.Off, rig.Pipeline!.ReviewCadenceOf("demo"));
    }

    [Fact]
    public void BEH_ORCH_13_With_the_review_switch_unset_no_run_starts_on_any_project_whatever_its_cadence()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: false, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Daily);

        var starts = RunsOver(rig, time, TimeSpan.FromDays(30));

        Assert.Empty(starts);
        Assert.Null(rig.Claim());
    }

    [Theory]
    [InlineData("daily", 1)]
    [InlineData("every-two-days", 2)]
    [InlineData("weekly", 7)]
    public void BEH_ORCH_13_A_project_on_a_cadence_starts_one_review_run_per_interval_and_never_more_often(string cadence, int days)
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadences.Parse(cadence));

        var starts = RunsOver(rig, time, TimeSpan.FromDays(30));

        // The first look starts one at once; then one each interval, hourly looks in between starting none. Over 30 days
        // that is one on each of days 0, d, 2d, ... below 30: ceiling of 30 over the interval.
        Assert.Equal((30 + days - 1) / days, starts.Count);
        foreach (var pair in starts.Zip(starts.Skip(1)))
        {
            Assert.Equal(TimeSpan.FromDays(days), pair.Second - pair.First);
        }
    }

    [Fact]
    public void BEH_ORCH_13_Setting_the_cadence_on_one_project_changes_no_other_and_a_new_project_starts_off()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.AddProject("other");

        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Weekly);

        Assert.Equal(ReviewCadence.Weekly, rig.Pipeline.ReviewCadenceOf("demo"));
        Assert.Equal(ReviewCadence.Off, rig.Pipeline.ReviewCadenceOf("other"));
        Assert.Equal(ReviewCadence.Off, rig.Pipeline.ReviewCadenceOf("a-project-added-later"));

        // Only demo's review runs: a second project with no cadence gets no run from the same look.
        Assert.NotNull(Tick(rig));
        Assert.Null(rig.Claim());
    }

    [Fact]
    public void BEH_ORCH_13_The_review_run_uses_the_review_model_and_asks_for_neglected_tickets_a_sample_and_the_architecture()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Daily);

        var claim = Assert.IsType<PoolClaim>(Tick(rig));

        Assert.Equal(OpusModel, claim.Model);
        Assert.Equal(PipelineTests.Models.Review, claim.Model);
        Assert.Contains("neglected tickets", claim.Task);
        Assert.Contains("a sample of finished work", claim.Task);
        Assert.Contains("the architecture", claim.Task);
    }

    [Fact]
    public void BEH_ORCH_13_The_review_run_pushes_no_work_branch_and_changes_no_code_and_writes_its_findings_as_notes()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Daily);

        var claim = Assert.IsType<PoolClaim>(Tick(rig));

        Assert.Contains("kit/work/", claim.Task);
        Assert.Contains("Do not push any kit/work/", claim.Task);
        Assert.Contains("Change nothing but", claim.Task);
        Assert.Contains("demo.notes.md", claim.Task);
        Assert.Contains("Write each finding as a note", claim.Task);

        // Its report changes no work item: no triage, spec or build is queued by it.
        rig.Finish(claim);
        Assert.Empty(rig.Pipeline.Items());
    }

    [Fact]
    public void BEH_ORCH_13_A_note_the_review_writes_is_picked_up_by_the_next_triage_like_any_other()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        // The review's own output: a plain note on the project's notes file, as a person would leave one.
        File.WriteAllText(Path.Combine(rig.Root, "demo.notes.md"),
            "# Notes on demo\n\n## 2026-11-16 09:00Z\n\nReview: the ticket for BEH-A has been waiting for a week with no question on it.\n");

        rig.Pipeline!.NoteLeft("demo");

        var triage = Assert.IsType<PoolClaim>(rig.Claim());
        Assert.Contains("Review: the ticket for BEH-A has been waiting for a week", triage.Task);
    }

    [Fact]
    public void BEH_ORCH_13_The_review_is_a_stage_of_the_pipeline_run_on_the_pool_and_not_a_custom_agent()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Daily);

        Tick(rig);

        // The run is the pipeline's own: the operator's, on the review stage, against the repo the spec names.
        var run = Assert.Single(rig.Store.List());
        Assert.Equal("review", run.Stage);
        Assert.Equal("demo", run.Project);
        Assert.Equal(PipelineTests.Models.Review, run.Model);
        Assert.Null(run.WorkItem);
    }

    [Fact]
    public void BEH_ORCH_13_The_settings_page_reads_each_project_cadence_and_the_switch()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);

        Assert.Equal(200, rig.Router.Route("POST", "/api/projects/demo/review", body: PipelineTests.Json(new { cadence = "every-two-days" })).Status);

        var read = rig.Router.Route("GET", "/api/settings/review");
        Assert.Equal(200, read.Status);
        var json = System.Text.Json.JsonSerializer.Serialize(read.Body);
        Assert.Contains("\"on\":true", json);
        Assert.Contains("\"demo\":\"every-two-days\"", json);
    }

    [Fact]
    public void BEH_ORCH_13_An_unknown_cadence_is_refused_and_changes_nothing()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);

        var r = rig.Router.Route("POST", "/api/projects/demo/review", body: PipelineTests.Json(new { cadence = "hourly" }));

        Assert.Equal(400, r.Status);
        Assert.Equal(ReviewCadence.Off, rig.Pipeline!.ReviewCadenceOf("demo"));
    }

    [Fact]
    public void BEH_ORCH_3_The_timer_starts_only_the_review_run_and_queues_no_triage_spec_or_build()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        rig.Pipeline!.SetReviewCadence("demo", ReviewCadence.Daily);

        RunsOver(rig, time, TimeSpan.FromDays(3));

        // No note was left and no user action queued anything: the only items are none at all.
        Assert.Empty(rig.Pipeline.Items());
        Assert.All(rig.Store.List(), r => Assert.Equal("review", r.Stage));
    }

    [Fact]
    public void BEH_SETTINGS_5_The_idle_choice_and_its_endpoint_are_unchanged_by_the_review_row()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        File.WriteAllText(Path.Combine(rig.Root, "kit.beh"), "# kit:repo o/r@dev\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
        var before = IdleOf(rig);

        Assert.Equal(200, rig.Router.Route("POST", "/api/projects/demo/review", body: PipelineTests.Json(new { cadence = "weekly" })).Status);

        var after = Assert.IsType<IdleSetting>(rig.Router.Route("GET", "/api/settings/idle").Body);
        Assert.Equal(before, after.Idle);
        Assert.Equal("health", after.Idle);
        Assert.Equal(["health"], after.Order);
    }

    [Fact]
    public void BEH_SETTINGS_6_The_ordered_idle_list_keeps_its_items_and_switches_and_gains_no_review_row()
    {
        var time = new PipelineTests.Ticking();
        using var rig = new PipelineTests.Rig(review: true, clock: time);
        File.WriteAllText(Path.Combine(rig.Root, "kit.beh"), "# kit:repo o/r@dev\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n");
        Assert.Equal(200, rig.Router.Route("POST", "/api/settings/idle", body: PipelineTests.Json(new { order = new[] { "ideas", "health" } })).Status);

        Assert.Equal(200, rig.Router.Route("POST", "/api/projects/demo/review", body: PipelineTests.Json(new { cadence = "daily" })).Status);

        var setting = Assert.IsType<IdleSetting>(rig.Router.Route("GET", "/api/settings/idle").Body);
        Assert.Equal(["ideas", "health"], setting.Order);
        Assert.Equal("ideas", setting.Idle);
        Assert.DoesNotContain(setting.Order, item => item.Contains("review", StringComparison.OrdinalIgnoreCase));
    }
}
