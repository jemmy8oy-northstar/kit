using Balenthiran.Kit.Services;
using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit-ui.beh <c>BEH-ORCH-1</c> (the triage task and its context bundle) and <c>BEH-ORCH-2</c> (the flag, and the
/// model for each stage as a setting). All pure: nothing here starts a run.
/// </summary>
public class OrchestrationStageTests
{
    private static ContextBundle Sample(string? flag = null) => new(
        "Runs show their stage",
        "run-stage-badge",
        ["BEH-USERS-9", "new: a stage badge on each run"],
        ["kit-ui.notes.md#2026-11-04-1807", "chat:abc"],
        ["backend/Balenthiran.Kit.Services/KitRouter.Runs.cs"],
        "The runs list is built in KitRouter.Runs.cs.\n\nCards: this line is just context.",
        flag);

    // ---- BEH-ORCH-1: the bundle ------------------------------------------------------------------

    [Fact]
    public void BEH_ORCH_1_A_bundle_renders_and_parses_back_to_the_same_bundle()
    {
        var parsed = ContextBundle.Parse(Sample().Render());

        Assert.True(parsed.Ok, parsed.Error);
        var b = parsed.Bundle!;
        Assert.Equal("Runs show their stage", b.Title);
        Assert.Equal("run-stage-badge", b.Slug);
        Assert.Equal(["BEH-USERS-9", "new: a stage badge on each run"], b.Cards);
        Assert.Equal(["kit-ui.notes.md#2026-11-04-1807", "chat:abc"], b.Notes);
        Assert.Equal(["backend/Balenthiran.Kit.Services/KitRouter.Runs.cs"], b.Files);
        Assert.Equal(Sample().Context, b.Context);
        Assert.Null(b.Flag);
    }

    [Fact]
    public void BEH_ORCH_2_A_flag_with_its_reason_survives_the_round_trip()
    {
        var parsed = ContextBundle.Parse(Sample("two cards disagree about who may start a run").Render());

        Assert.True(parsed.Ok, parsed.Error);
        Assert.Equal("two cards disagree about who may start a run", parsed.Bundle!.Flag);
    }

    [Fact]
    public void BEH_ORCH_2_A_flag_without_a_reason_is_refused()
    {
        var text = Sample("x").Render().Replace("Flag: x", "Flag:");

        var parsed = ContextBundle.Parse(text);

        Assert.False(parsed.Ok);
        Assert.Contains("flag", parsed.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BEH_ORCH_1_A_bundle_with_no_slug_is_refused_with_the_reason()
    {
        var parsed = ContextBundle.Parse(Sample().Render().Replace("Work-item: run-stage-badge", "Work-item:"));

        Assert.False(parsed.Ok);
        Assert.Contains("slug", parsed.Error);
    }

    [Theory]
    [InlineData("Run Stage")]
    [InlineData("run_stage")]
    [InlineData("../escape")]
    [InlineData("-leading")]
    [InlineData("double--hyphen")]
    public void BEH_ORCH_1_A_slug_that_is_not_safe_in_a_branch_name_is_refused(string slug)
    {
        var parsed = ContextBundle.Parse(Sample().Render().Replace("run-stage-badge", slug));

        Assert.False(parsed.Ok);
        Assert.Contains("slug", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_1_A_bundle_with_an_unknown_section_is_refused_naming_it()
    {
        var parsed = ContextBundle.Parse(Sample().Render().Replace("Files:", "Tests:"));

        Assert.False(parsed.Ok);
        Assert.Contains("Tests:", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_1_A_bundle_with_sections_out_of_order_is_refused()
    {
        var text = "# t\nWork-item: a-b\nNotes:\n- n\nCards:\n- BEH-X-1\nFiles:\nContext:\n";

        var parsed = ContextBundle.Parse(text);

        Assert.False(parsed.Ok);
        Assert.Contains("Cards:", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_1_A_bundle_with_no_cards_is_refused()
    {
        var parsed = ContextBundle.Parse("# t\nWork-item: a-b\nCards:\nNotes:\n- n\nFiles:\nContext:\nx\n");

        Assert.False(parsed.Ok);
        Assert.Contains("Cards is empty", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_1_A_card_that_is_neither_an_id_nor_new_is_refused()
    {
        var parsed = ContextBundle.Parse(Sample().Render().Replace("- BEH-USERS-9", "- the runs one"));

        Assert.False(parsed.Ok);
        Assert.Contains("the runs one", parsed.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just some prose")]
    [InlineData("# only a title")]
    public void BEH_ORCH_1_Text_that_is_not_a_bundle_is_an_error_not_an_exception(string text)
    {
        var parsed = ContextBundle.Parse(text);

        Assert.False(parsed.Ok);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Fact]
    public void BEH_ORCH_1_Rendering_a_bundle_that_could_not_be_parsed_throws()
    {
        var badSlug = Sample() with { Slug = "Bad Slug" };
        var noCards = Sample() with { Cards = [] };
        Assert.Throws<InvalidOperationException>(() => badSlug.Render());
        Assert.Throws<InvalidOperationException>(() => noCards.Render());
    }

    // ---- BEH-ORCH-1: the task --------------------------------------------------------------------

    private static readonly (string, string)[] Notes =
    [
        ("kit-ui.notes.md#2026-11-04-1807", "make the run list show which stage it is"),
        ("chat:abc", "and tell me when a stage is flagged"),
    ];

    private static readonly (string, string)[] Cards =
    [
        ("BEH-ORCH-1", "New notes and chat are triaged"),
        ("BEH-USERS-9", "Agent runs"),
    ];

    [Fact]
    public void BEH_ORCH_1_The_task_names_the_branch_and_the_bundle_path_and_the_project()
    {
        var task = TriageStage.BuildTask("kit", Notes, Cards);

        Assert.Contains("\"kit\"", task);
        Assert.Contains("kit/work/<work-item-slug>", task);
        Assert.Contains(".kit/bundles/<work-item-slug>.md", task);
        Assert.Equal("kit/work/run-stage-badge", TriageStage.Branch("run-stage-badge"));
        Assert.Equal(".kit/bundles/run-stage-badge.md", TriageStage.BundlePath("run-stage-badge"));
    }

    [Fact]
    public void BEH_ORCH_1_The_task_carries_every_note_ref_and_text_and_every_card()
    {
        var task = TriageStage.BuildTask("kit", Notes, Cards);

        foreach (var (reference, text) in Notes)
        {
            Assert.Contains(reference, task);
            Assert.Contains(text, task);
        }

        Assert.Contains("BEH-ORCH-1: New notes and chat are triaged", task);
        Assert.Contains("BEH-USERS-9: Agent runs", task);
        Assert.DoesNotContain("TRUNCATED", task);
    }

    [Fact]
    public void BEH_ORCH_1_The_task_tells_the_run_to_stop_after_pushing_and_not_to_write_specs()
    {
        var task = TriageStage.BuildTask("kit", Notes, Cards);

        Assert.Contains("push", task);
        Assert.Contains("STOP", task);
        Assert.Contains("Do NOT write or edit any card", task);
    }

    [Fact]
    public void BEH_ORCH_1_A_long_note_is_truncated_and_the_task_says_so()
    {
        var long_ = new string('x', TriageStage.MaxNoteChars + 500);

        var task = TriageStage.BuildTask("kit", [("kit-ui.notes.md#big", long_)], Cards);

        Assert.DoesNotContain(long_, task);
        Assert.Contains(new string('x', TriageStage.MaxNoteChars), task);
        Assert.Contains("[truncated: 500 more characters, read kit-ui.notes.md#big", task);
        Assert.Contains("TRUNCATED", task);
    }

    [Fact]
    public void BEH_ORCH_1_Many_notes_stay_within_the_total_cap_and_every_ref_is_still_listed()
    {
        var notes = Enumerable.Range(0, 20).Select(i => ($"n.md#{i:D2}", new string('§', TriageStage.MaxNoteChars))).ToArray();

        var task = TriageStage.BuildTask("kit", notes, Cards);

        Assert.Equal(TriageStage.MaxTotalNoteChars, task.Count(c => c == '§'));
        foreach (var (reference, _) in notes)
        {
            Assert.Contains($"### {reference}", task);
        }

        Assert.Contains("TRUNCATED", task);
    }

    [Fact]
    public void BEH_ORCH_1_The_format_the_task_describes_is_one_the_parser_accepts()
    {
        var task = TriageStage.BuildTask("kit", Notes, Cards);
        var start = task.IndexOf("```\n", StringComparison.Ordinal) + 4;
        var shape = task[start..task.IndexOf("```\n", start, StringComparison.Ordinal)];

        foreach (var section in new[] { "Work-item:", "Cards:", "Notes:", "Files:", "Context:" })
        {
            Assert.Contains(section, shape);
        }
    }

    // ---- BEH-ORCH-2: the review ------------------------------------------------------------------

    private static ContextBundle Reviewed(ReviewOutcome outcome, string note = "checked the files") =>
        Sample("two cards disagree") with { Review = new BundleReview(outcome, note) };

    [Theory]
    [InlineData(ReviewOutcome.Agreed, "agreed")]
    [InlineData(ReviewOutcome.Revised, "revised")]
    [InlineData(ReviewOutcome.Escalated, "escalated")]
    public void BEH_ORCH_2_A_review_survives_the_round_trip_straight_after_the_flag(ReviewOutcome outcome, string word)
    {
        var text = Reviewed(outcome, "who may start a run? — needs a person").Render();

        Assert.Contains($"Flag: two cards disagree\nReview: {word} — who may start a run? — needs a person\nCards:", text);
        var parsed = ContextBundle.Parse(text);
        Assert.True(parsed.Ok, parsed.Error);
        Assert.Equal("two cards disagree", parsed.Bundle!.Flag);
        Assert.Equal(new BundleReview(outcome, "who may start a run? — needs a person"), parsed.Bundle.Review);
    }

    [Fact]
    public void BEH_ORCH_2_A_review_without_the_flag_it_answers_is_refused()
    {
        var bundle = Sample() with { Review = new BundleReview(ReviewOutcome.Agreed, "fine") };

        Assert.Equal("a review needs the flag it answers", bundle.Check());
        Assert.Throws<InvalidOperationException>(() => bundle.Render());
        var parsed = ContextBundle.Parse(Reviewed(ReviewOutcome.Agreed).Render().Replace("Flag: two cards disagree\n", ""));
        Assert.False(parsed.Ok);
        Assert.Contains("needs the flag", parsed.Error);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("Agreed")]
    [InlineData("1")]
    [InlineData("")]
    public void BEH_ORCH_2_A_review_with_an_unknown_outcome_word_is_refused(string word)
    {
        var text = Reviewed(ReviewOutcome.Agreed).Render().Replace("Review: agreed", $"Review: {word}");

        var parsed = ContextBundle.Parse(text);

        Assert.False(parsed.Ok);
        Assert.Contains($"outcome \"{word}\"", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_2_A_review_outside_the_enum_is_refused()
    {
        var bundle = Sample("x") with { Review = new BundleReview((ReviewOutcome)99, "fine") };

        Assert.Contains("agreed, revised or escalated", bundle.Check());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one\ntwo")]
    [InlineData("one\r\ntwo")]
    public void BEH_ORCH_2_A_review_note_that_is_empty_or_spans_lines_is_refused(string note)
    {
        var bundle = Reviewed(ReviewOutcome.Revised, note);

        Assert.Equal("the review needs a one-line note", bundle.Check());
        Assert.Throws<InvalidOperationException>(() => bundle.Render());
    }

    [Fact]
    public void BEH_ORCH_2_A_review_line_with_no_note_is_refused_when_read_back()
    {
        var parsed = ContextBundle.Parse(Reviewed(ReviewOutcome.Agreed).Render().Replace("Review: agreed — checked the files", "Review: agreed"));

        Assert.False(parsed.Ok);
        Assert.Contains("one-line note", parsed.Error);
    }

    [Fact]
    public void BEH_ORCH_2_A_review_task_needs_a_flagged_bundle_nobody_has_reviewed()
    {
        var unflagged = Assert.Throws<InvalidOperationException>(() => ReviewStage.BuildTask("kit", Sample()));
        var done = Assert.Throws<InvalidOperationException>(() => ReviewStage.BuildTask("kit", Reviewed(ReviewOutcome.Agreed)));

        Assert.Contains("flagged", unflagged.Message);
        Assert.Contains("already", done.Message);
    }

    [Fact]
    public void BEH_ORCH_2_The_review_task_carries_the_flag_the_branch_the_path_and_the_bundle()
    {
        var bundle = Sample("two cards disagree about who may start a run");

        var task = ReviewStage.BuildTask("kit", bundle);

        Assert.Contains("\"kit\"", task);
        Assert.Contains("reason \"two cards disagree about who may start a run\"", task);
        Assert.Contains("branch kit/work/run-stage-badge", task);
        Assert.Contains("Edit .kit/bundles/run-stage-badge.md", task);
        Assert.Contains("```\n" + bundle.Render() + "```", task);
        Assert.Contains("ONLY the files it lists under Files", task);
        Assert.Contains("Do NOT search the repo", task);
        Assert.Contains("Review: agreed|revised|escalated — <one-line note>", task);
        Assert.Contains("Keep the Flag line", task);
        Assert.Contains("STOP", task);
        Assert.Contains("do not open a pull request", task);
    }

    [Fact]
    public void BEH_ORCH_2_The_tally_counts_flagged_bundles_by_outcome_and_ignores_unflagged_ones()
    {
        var tally = ReviewStage.Tally(
        [
            Sample(),
            Sample() with { Slug = "other" },
            Reviewed(ReviewOutcome.Agreed),
            Reviewed(ReviewOutcome.Agreed),
            Reviewed(ReviewOutcome.Revised),
            Reviewed(ReviewOutcome.Escalated),
            Sample("still waiting"),
        ]);

        Assert.Equal(new ReviewTally(Flagged: 5, Awaiting: 1, Agreed: 2, Revised: 1, Escalated: 1), tally);
        Assert.Equal(new ReviewTally(0, 0, 0, 0, 0), ReviewStage.Tally([Sample()]));
    }

    // ---- BEH-ORCH-2: the model per stage ---------------------------------------------------------

    [Fact]
    public void BEH_ORCH_2_Stage_models_default_to_haiku_for_triage_and_opus_for_review()
    {
        Assert.Equal("claude-haiku-5-5", KitSettings.ParseStageModelTriage(null));
        Assert.Equal("claude-opus-5-5", KitSettings.ParseStageModelReview("  "));
        Assert.Equal("claude-haiku-5-5", new KitSettings("d", "r", null, "dist").StageModelTriage);
        Assert.Equal("claude-opus-5-5", new KitSettings("d", "r", null, "dist").StageModelReview);
    }

    [Fact]
    public void BEH_ORCH_2_A_stage_model_is_read_from_the_environment_or_refused()
    {
        Assert.Equal("claude-sonnet-5-5", KitSettings.ParseStageModelTriage(" claude-sonnet-5-5 "));
        Assert.Equal("claude-fable-1", KitSettings.ParseStageModelReview("claude-fable-1"));
        foreach (var bad in new[] { "has space", "-leading", "a/b", "x;rm", new string('a', 65) })
        {
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelTriage(bad));
            Assert.Throws<InvalidOperationException>(() => KitSettings.ParseStageModelReview(bad));
        }
    }
}
