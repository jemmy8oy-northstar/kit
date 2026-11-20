using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-3</c>: the orchestrator as a pure state machine over work items, and the spec and build
/// stage models. All pure: nothing here starts a run.
/// </summary>
public class OrchestratorTests
{
    private static ContextBundle Bundle(string? flag = null, BundleReview? review = null) => new(
        "Runs show their stage",
        "run-stage-badge",
        ["BEH-USERS-9"],
        [],
        ["backend/Balenthiran.Kit.Services/KitRouter.Runs.cs"],
        "The runs list is built in KitRouter.Runs.cs.",
        flag,
        review);

    private static WorkItem Item(PipelineStage stage, WorkItemStatus status, string project = "kit", string slug = "run-stage-badge", PipelineStage? reviewed = null) =>
        new(project, slug, stage, status, reviewed);

    private static StageResult Ok(PipelineStage stage, ContextBundle? bundle = null) => new(stage, true, bundle ?? Bundle(), null);

    private static StageResult Flagged(PipelineStage stage) => new(stage, true, Bundle("the card is ambiguous"), null);

    private static StageResult Reviewed(ReviewOutcome outcome, string note = "fine") =>
        new(PipelineStage.Review, true, Bundle("the card is ambiguous", new BundleReview(outcome, note)), null);

    // ---- BEH-ORCH-3: advancing an item -----------------------------------------------------------

    [Fact]
    public void BEH_ORCH_3_An_unflagged_item_runs_triage_then_spec_then_build_then_is_done()
    {
        var item = Item(PipelineStage.Triage, WorkItemStatus.Queued);

        item = Orchestrator.Advance(Orchestrator.Start(item), Ok(PipelineStage.Triage));
        Assert.Equal(Item(PipelineStage.Spec, WorkItemStatus.Queued), item);

        item = Orchestrator.Advance(Orchestrator.Start(item), Ok(PipelineStage.Spec));
        Assert.Equal(Item(PipelineStage.Build, WorkItemStatus.Queued), item);

        item = Orchestrator.Advance(Orchestrator.Start(item), Ok(PipelineStage.Build));
        Assert.Equal(Item(PipelineStage.Build, WorkItemStatus.Done), item);
    }

    [Fact]
    public void BEH_ORCH_3_A_build_with_no_bundle_is_still_done()
    {
        var done = Orchestrator.Advance(Item(PipelineStage.Build, WorkItemStatus.Running), new StageResult(PipelineStage.Build, true, null, null));

        Assert.Equal(WorkItemStatus.Done, done.Status);
        Assert.Equal(PipelineStage.Build, done.Stage);
        Assert.Null(done.StoppedBecause);
    }

    [Theory]
    [InlineData(PipelineStage.Triage)]
    [InlineData(PipelineStage.Spec)]
    public void BEH_ORCH_3_A_triage_or_spec_with_no_bundle_stops_the_item(PipelineStage stage)
    {
        var stopped = Orchestrator.Advance(Item(stage, WorkItemStatus.Running), new StageResult(stage, true, null, null));

        Assert.Equal(WorkItemStatus.Stopped, stopped.Status);
        Assert.Equal(stage, stopped.Stage);
        Assert.Equal("the stage left no bundle", stopped.StoppedBecause);
    }

    [Theory]
    [InlineData(PipelineStage.Triage)]
    [InlineData(PipelineStage.Spec)]
    [InlineData(PipelineStage.Build)]
    public void BEH_ORCH_3_A_flag_at_any_stage_queues_a_review_of_that_stage(PipelineStage stage)
    {
        var next = Orchestrator.Advance(Item(stage, WorkItemStatus.Running), Flagged(stage));

        Assert.Equal(Item(PipelineStage.Review, WorkItemStatus.Queued, reviewed: stage), next);
    }

    [Theory]
    [InlineData(ReviewOutcome.Agreed)]
    [InlineData(ReviewOutcome.Revised)]
    public void BEH_ORCH_3_An_agreed_or_revised_review_resumes_at_the_stage_after_the_flagged_one(ReviewOutcome outcome)
    {
        var inReview = Item(PipelineStage.Review, WorkItemStatus.Running, reviewed: PipelineStage.Spec);

        Assert.Equal(Item(PipelineStage.Build, WorkItemStatus.Queued), Orchestrator.Advance(inReview, Reviewed(outcome)));
        Assert.Equal(
            Item(PipelineStage.Spec, WorkItemStatus.Queued),
            Orchestrator.Advance(inReview with { ReviewedStage = PipelineStage.Triage }, Reviewed(outcome)));
        Assert.Equal(
            Item(PipelineStage.Build, WorkItemStatus.Done),
            Orchestrator.Advance(inReview with { ReviewedStage = PipelineStage.Build }, Reviewed(outcome)));
    }

    [Fact]
    public void BEH_ORCH_3_A_flagged_spec_goes_to_review_and_an_agreed_review_resumes_at_build()
    {
        var item = Item(PipelineStage.Spec, WorkItemStatus.Queued);

        item = Orchestrator.Advance(Orchestrator.Start(item), Flagged(PipelineStage.Spec));
        Assert.Equal(PipelineStage.Review, item.Stage);
        Assert.Equal(PipelineStage.Spec, item.ReviewedStage);

        item = Orchestrator.Advance(Orchestrator.Start(item), Reviewed(ReviewOutcome.Agreed));
        Assert.Equal(Item(PipelineStage.Build, WorkItemStatus.Queued), item);
        Assert.Null(item.ReviewedStage);
    }

    [Fact]
    public void BEH_ORCH_3_An_escalated_review_stops_the_item_with_the_review_note()
    {
        var stopped = Orchestrator.Advance(
            Item(PipelineStage.Review, WorkItemStatus.Running, reviewed: PipelineStage.Spec),
            Reviewed(ReviewOutcome.Escalated, "which of the two cards wins?"));

        Assert.Equal(WorkItemStatus.Stopped, stopped.Status);
        Assert.Equal(PipelineStage.Review, stopped.Stage);
        Assert.Equal("which of the two cards wins?", stopped.StoppedBecause);
    }

    [Fact]
    public void BEH_ORCH_3_A_review_that_left_no_review_line_stops_the_item()
    {
        var inReview = Item(PipelineStage.Review, WorkItemStatus.Running, reviewed: PipelineStage.Triage);

        var noLine = Orchestrator.Advance(inReview, new StageResult(PipelineStage.Review, true, Bundle("the card is ambiguous"), null));
        var noBundle = Orchestrator.Advance(inReview, new StageResult(PipelineStage.Review, true, null, null));

        Assert.Equal(WorkItemStatus.Stopped, noLine.Status);
        Assert.Equal("the review left no Review line", noLine.StoppedBecause);
        Assert.Equal(WorkItemStatus.Stopped, noBundle.Status);
        Assert.Equal("the review left no Review line", noBundle.StoppedBecause);
    }

    [Fact]
    public void BEH_ORCH_3_A_failed_run_stops_the_item_with_the_error()
    {
        var stopped = Orchestrator.Advance(
            Item(PipelineStage.Build, WorkItemStatus.Running),
            new StageResult(PipelineStage.Build, false, Bundle(), "the tests never passed"));

        Assert.Equal(WorkItemStatus.Stopped, stopped.Status);
        Assert.Equal(PipelineStage.Build, stopped.Stage);
        Assert.Equal("the tests never passed", stopped.StoppedBecause);
    }

    [Fact]
    public void BEH_ORCH_3_A_result_for_another_stage_or_for_an_item_that_is_not_running_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Orchestrator.Advance(Item(PipelineStage.Spec, WorkItemStatus.Running), Ok(PipelineStage.Triage)));
        Assert.Equal("the item is running Spec but the result is for Triage", ex.Message);

        foreach (var status in new[] { WorkItemStatus.Queued, WorkItemStatus.Stopped, WorkItemStatus.Done })
        {
            var notRunning = Assert.Throws<InvalidOperationException>(() =>
                Orchestrator.Advance(Item(PipelineStage.Triage, status), Ok(PipelineStage.Triage)));
            Assert.Equal($"only a running item can finish a stage, not one that is {status}", notRunning.Message);
        }
    }

    // ---- BEH-ORCH-3: one orchestrator per project ------------------------------------------------

    [Fact]
    public void BEH_ORCH_3_Startable_gives_one_queued_item_per_project_in_queue_order()
    {
        var a1 = Item(PipelineStage.Triage, WorkItemStatus.Queued, "alpha", "a-one");
        var b1 = Item(PipelineStage.Spec, WorkItemStatus.Queued, "beta", "b-one");
        var a2 = Item(PipelineStage.Triage, WorkItemStatus.Queued, "alpha", "a-two");
        var b2 = Item(PipelineStage.Build, WorkItemStatus.Queued, "beta", "b-two");

        Assert.Equal([a1, b1], Orchestrator.Startable([a1, b1, a2, b2]));
        Assert.Equal([b2, a2], Orchestrator.Startable([b2, a2, b1, a1]));
    }

    [Fact]
    public void BEH_ORCH_3_A_project_with_a_running_item_waits_and_other_projects_are_not_held_up()
    {
        var running = Item(PipelineStage.Spec, WorkItemStatus.Running, "alpha", "a-one");
        var waiting = Item(PipelineStage.Triage, WorkItemStatus.Queued, "alpha", "a-two");
        var other = Item(PipelineStage.Triage, WorkItemStatus.Queued, "beta", "b-one");

        Assert.Equal([other], Orchestrator.Startable([waiting, running, other]));
        Assert.Empty(Orchestrator.Startable([running, waiting]));
    }

    [Fact]
    public void BEH_ORCH_3_Stopped_and_done_items_are_never_startable_and_do_not_hold_a_project()
    {
        var stopped = Item(PipelineStage.Review, WorkItemStatus.Stopped, "alpha", "a-one");
        var done = Item(PipelineStage.Build, WorkItemStatus.Done, "alpha", "a-two");
        var queued = Item(PipelineStage.Spec, WorkItemStatus.Queued, "alpha", "a-four");

        Assert.Equal([queued], Orchestrator.Startable([stopped, done, queued]));
        Assert.Empty(Orchestrator.Startable([]));
    }

    [Fact]
    public void BEH_ORCH_3_Start_runs_a_queued_item_and_refuses_any_other()
    {
        Assert.Equal(
            Item(PipelineStage.Spec, WorkItemStatus.Running),
            Orchestrator.Start(Item(PipelineStage.Spec, WorkItemStatus.Queued)));

        foreach (var status in new[] { WorkItemStatus.Running, WorkItemStatus.Stopped, WorkItemStatus.Done })
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Orchestrator.Start(Item(PipelineStage.Spec, status)));
            Assert.Equal($"only a queued item can start, not one that is {status}", ex.Message);
        }
    }

    // ---- BEH-ORCH-5: the turn limit per stage ----------------------------------------------------

    [Fact]
    public void BEH_ORCH_5_Stage_turn_limits_default_to_30_20_30_and_100()
    {
        var turns = StageTurnSettings.Defaults;

        Assert.Equal(30, Orchestrator.TurnsFor(PipelineStage.Triage, turns));
        Assert.Equal(20, Orchestrator.TurnsFor(PipelineStage.Review, turns));
        Assert.Equal(30, Orchestrator.TurnsFor(PipelineStage.Spec, turns));
        Assert.Equal(100, Orchestrator.TurnsFor(PipelineStage.Build, turns));
        var settings = new KitSettings("d", "r", null, "dist");
        Assert.Equal((30, 20, 30, 100), (settings.StageTurnsTriage, settings.StageTurnsReview, settings.StageTurnsSpec, settings.StageTurnsBuild));
        Assert.Equal((30, 20, 30, 100), (KitSettings.ParseStageTurnsTriage(null), KitSettings.ParseStageTurnsReview(""), KitSettings.ParseStageTurnsSpec("  "), KitSettings.ParseStageTurnsBuild(null)));
    }

    [Fact]
    public void BEH_ORCH_5_Each_stage_runs_on_its_own_turn_limit()
    {
        var turns = new StageTurnSettings(1, 2, 3, 4);

        Assert.Equal(1, Orchestrator.TurnsFor(PipelineStage.Triage, turns));
        Assert.Equal(2, Orchestrator.TurnsFor(PipelineStage.Review, turns));
        Assert.Equal(3, Orchestrator.TurnsFor(PipelineStage.Spec, turns));
        Assert.Equal(4, Orchestrator.TurnsFor(PipelineStage.Build, turns));
    }

    [Fact]
    public void BEH_ORCH_5_A_turn_limit_is_read_from_the_environment_or_refused()
    {
        Assert.Equal(7, KitSettings.ParseStageTurnsTriage(" 7 "));
        Assert.Equal(250, KitSettings.ParseStageTurnsBuild("250"));
        foreach (var bad in new[] { "0", "-5", "1.5", "ten", "7 turns", "+3", "99999999999" })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsTriage(bad));
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsReview(bad));
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsSpec(bad));
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageTurnsBuild(bad));
        }
    }

    // ---- BEH-ORCH-3: the model per stage ---------------------------------------------------------

    [Fact]
    public void BEH_ORCH_3_Review_runs_on_the_review_model_and_spec_and_build_on_the_work_models()
    {
        var models = new StageModelSettings("m-triage", "m-review", "m-spec", "m-build");

        Assert.Equal("m-triage", Orchestrator.ModelFor(PipelineStage.Triage, models));
        Assert.Equal("m-review", Orchestrator.ModelFor(PipelineStage.Review, models));
        Assert.Equal("m-spec", Orchestrator.ModelFor(PipelineStage.Spec, models));
        Assert.Equal("m-build", Orchestrator.ModelFor(PipelineStage.Build, models));
    }

    [Fact]
    public void BEH_ORCH_3_Spec_and_build_models_default_to_haiku()
    {
        Assert.Equal("claude-haiku-5-5", KitSettings.ParseStageModelSpec(null));
        Assert.Equal("claude-haiku-5-5", KitSettings.ParseStageModelBuild("  "));
        Assert.Equal("claude-haiku-5-5", new KitSettings("d", "r", null, "dist").StageModelSpec);
        Assert.Equal("claude-haiku-5-5", new KitSettings("d", "r", null, "dist").StageModelBuild);
    }

    [Fact]
    public void BEH_ORCH_3_A_spec_or_build_model_is_read_from_the_environment_or_refused()
    {
        Assert.Equal("claude-sonnet-5-5", KitSettings.ParseStageModelSpec(" claude-sonnet-5-5 "));
        Assert.Equal("claude-fable-1", KitSettings.ParseStageModelBuild("claude-fable-1"));
        foreach (var bad in new[] { "has space", "-leading", "a/b", "x;rm", new string('a', 65) })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelSpec(bad));
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelBuild(bad));
        }
    }
}
