using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:switch FEAT-X operator|testers on|off</c> (BEH-FLAGS-1, BEH-FLAGS-2): Kit's features
/// switched on and off for him and for testers — stored as a comment in Kit's primary file, written
/// by the operator alone, and served to the UI as what is off for the caller's group.
/// </summary>
public class FlagsTests
{
    private const string Plain =
        "# kit:project kit\n# kit:repo o/r@dev\n\nfeature FEAT-A \"Alpha\"\n  stage ux\n\nfeature FEAT-B \"Beta\"\n  stage ux\n\nbehaviour BEH-1 \"a thing\"\n  in FEAT-A\n  when opens page:Home\n";

    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";

    private readonly CorpusWriter _writer = new(new CorpusParser());

    private static (string Dir, KitRouter Router) Rig(bool accounts, string text = Plain)
    {
        var dir = Directory.CreateTempSubdirectory("kit-flags-").FullName;
        File.WriteAllText(Path.Combine(dir, "kit.beh"), text);

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), accounts ? Operator : null,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: accounts ? users : null);
        return (dir, router);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static string SignIn(KitRouter router, string? name, string password)
    {
        var r = router.Route("POST", "/api/session", body: Json(name is null ? new { password } : (object)new { username = name, password }));
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    private static IFeatureSwitches Served(KitRouter router, string? cookie = null)
    {
        var r = router.Route("GET", "/api/settings/features", cookie);
        Assert.Equal(200, r.Status);
        return Assert.IsType<FeatureSwitches>(r.Body);
    }

    private static IKitResponse Set(KitRouter router, string feature, string group, bool on, string? cookie = null) =>
        router.Route("POST", "/api/settings/features", cookie, body: Json(new { feature, group, on }));

    [Fact]
    public void BEH_FLAGS_1_no_line_means_on_for_him_and_off_for_testers()
    {
        var switches = SwitchDirective.Read(Plain);

        Assert.Empty(switches);
        Assert.True(SwitchDirective.IsOn(switches, "FEAT-A", "operator"));
        Assert.False(SwitchDirective.IsOn(switches, "FEAT-A", "testers"));
    }

    [Theory]
    [InlineData("# kit:switch FEAT-A testers on", "testers", true)]
    [InlineData("# kit:switch FEAT-A operator off", "operator", false)]
    [InlineData("#   kit:switch   FEAT-A   operator   off  ", "operator", false)]
    public void BEH_FLAGS_1_a_line_overrides_the_default(string line, string group, bool expected)
    {
        var s = SwitchDirective.Read(line + "\n" + Plain);

        Assert.Equal(expected, SwitchDirective.IsOn(s, "FEAT-A", group));
    }

    [Theory]
    [InlineData("# kit:switch FEAT-A testers")]
    [InlineData("# kit:switch FEAT-A testers maybe")]
    [InlineData("# kit:switch FEAT-A everyone on")]
    [InlineData("# kit:switch feat-a testers on")]
    [InlineData("# kit:switch FEAT-A testers on extra")]
    [InlineData("# kit:switches FEAT-A testers on")]
    public void BEH_FLAGS_1_a_malformed_line_is_skipped_so_the_default_stands(string line)
    {
        Assert.Empty(SwitchDirective.Read(line + "\n" + Plain));
    }

    [Fact]
    public void BEH_FLAGS_1_the_first_good_line_for_a_feature_and_group_wins()
    {
        var s = SwitchDirective.Read("# kit:switch FEAT-A testers on\n# kit:switch FEAT-A testers off\n" + Plain);

        Assert.True(SwitchDirective.IsOn(s, "FEAT-A", "testers"));
    }

    [Fact]
    public void BEH_FLAGS_1_the_parser_ignores_the_line_as_a_comment_so_main_reads_the_file_unchanged()
    {
        var parser = new CorpusParser();
        var with = _writer.SetSwitch(Plain, "FEAT-A", "testers", true).Text!;

        Assert.Contains("# kit:switch FEAT-A testers on\n", with, StringComparison.Ordinal);
        Assert.Equal(
            parser.ParseCorpus(Plain, "kit").Features.Select(f => f.Id),
            parser.ParseCorpus(with, "kit").Features.Select(f => f.Id));
        Assert.Equal(
            parser.ParseCorpus(Plain, "kit").Behaviours.Select(b => b.Id),
            parser.ParseCorpus(with, "kit").Behaviours.Select(b => b.Id));
    }

    [Fact]
    public void BEH_FLAGS_1_writing_a_switch_adds_one_line_and_leaves_every_other_line_byte_identical()
    {
        var with = _writer.SetSwitch(Plain, "FEAT-A", "testers", true).Text!;

        var before = Plain.Split('\n').ToList();
        var after = with.Split('\n').ToList();
        Assert.Equal(before.Count + 1, after.Count);
        Assert.Equal("# kit:switch FEAT-A testers on", after[2]);
        after.RemoveAt(2);
        Assert.Equal(before, after);
    }

    [Fact]
    public void BEH_FLAGS_1_a_second_switch_goes_after_the_first_and_a_change_replaces_in_place()
    {
        var one = _writer.SetSwitch(Plain, "FEAT-A", "testers", true).Text!;
        var two = _writer.SetSwitch(one, "FEAT-B", "testers", true).Text!;

        Assert.Equal("# kit:project kit\n# kit:repo o/r@dev\n# kit:switch FEAT-A testers on\n# kit:switch FEAT-B testers on\n", string.Join("", two.Split('\n').Take(4).Select(l => l + "\n")));

        var off = _writer.SetSwitch(two, "FEAT-A", "operator", false).Text!;
        Assert.Contains("# kit:switch FEAT-A operator off\n", off, StringComparison.Ordinal);
        Assert.Equal(two.Split('\n').Length + 1, off.Split('\n').Length);
    }

    [Fact]
    public void BEH_FLAGS_1_setting_back_to_the_default_removes_the_line_and_the_file_is_as_it_was()
    {
        var on = _writer.SetSwitch(Plain, "FEAT-A", "testers", true).Text!;

        Assert.Equal(Plain, _writer.SetSwitch(on, "FEAT-A", "testers", false).Text);

        var off = _writer.SetSwitch(Plain, "FEAT-B", "operator", false).Text!;
        Assert.Equal(Plain, _writer.SetSwitch(off, "FEAT-B", "operator", true).Text);
    }

    [Fact]
    public void BEH_FLAGS_1_setting_what_the_file_already_says_changes_nothing()
    {
        var on = _writer.SetSwitch(Plain, "FEAT-A", "testers", true).Text!;

        Assert.Equal(on, _writer.SetSwitch(on, "FEAT-A", "testers", true).Text);
        Assert.Equal(Plain, _writer.SetSwitch(Plain, "FEAT-A", "testers", false).Text);
        Assert.Equal(Plain, _writer.SetSwitch(Plain, "FEAT-A", "operator", true).Text);
    }

    [Fact]
    public void BEH_FLAGS_1_a_repeated_line_is_collapsed_to_one_good_one()
    {
        var messy = "# kit:switch FEAT-A testers on\n# kit:switch FEAT-A testers off\n" + Plain;

        var fixedUp = _writer.SetSwitch(messy, "FEAT-A", "testers", true).Text!;

        Assert.Equal(1, fixedUp.Split("kit:switch").Length - 1);
        Assert.True(SwitchDirective.IsOn(SwitchDirective.Read(fixedUp), "FEAT-A", "testers"));
    }

    [Theory]
    [InlineData("nonsense", "testers")]
    [InlineData("FEAT-A", "everyone")]
    public void BEH_FLAGS_1_the_writer_refuses_an_unknown_feature_shape_or_group(string feature, string group)
    {
        Assert.False(_writer.SetSwitch(Plain, feature, group, true).Ok);
    }

    [Fact]
    public void BEH_FLAGS_1_the_operator_sees_every_feature_with_both_switches()
    {
        var (_, router) = Rig(false, "# kit:switch FEAT-A testers on\n# kit:switch FEAT-B operator off\n" + Plain);

        var s = Served(router);

        Assert.Equal("operator", s.Group);
        Assert.Equal(["FEAT-B"], s.Off);
        Assert.Equal(["FEAT-A:True:True", "FEAT-B:False:False"], s.Features!.Select(f => $"{f.Id}:{f.Operator}:{f.Testers}"));
    }

    [Fact]
    public void BEH_FLAGS_1_a_feature_in_another_file_of_the_kit_project_is_listed_too()
    {
        var (dir, router) = Rig(false);
        File.WriteAllText(Path.Combine(dir, "kit-ui.beh"), "# kit:project kit\n\nfeature FEAT-UI \"Ui\"\n  stage ux\n");
        File.WriteAllText(Path.Combine(dir, "other.beh"), "feature FEAT-OTHER \"Not Kit\"\n  stage ux\n");

        Assert.Equal(["FEAT-A", "FEAT-B", "FEAT-UI"], Served(router).Features!.Select(f => f.Id).Order());
    }

    [Fact]
    public void BEH_FLAGS_2_a_tester_is_served_what_is_off_for_testers_and_not_the_table()
    {
        var (_, router) = Rig(true, "# kit:switch FEAT-A testers on\n" + Plain);
        var ann = SignIn(router, "ann", AnnPassword);

        var s = Served(router, ann);

        Assert.Equal("testers", s.Group);
        Assert.Equal(["FEAT-B"], s.Off);
        Assert.Null(s.Features);
    }

    [Fact]
    public void BEH_FLAGS_2_a_caller_with_no_session_on_a_locked_kit_is_a_tester()
    {
        var (_, router) = Rig(true);

        Assert.Equal("testers", Served(router).Group);
        Assert.Equal("operator", Served(router, SignIn(router, null, Operator)).Group);
    }

    [Fact]
    public void BEH_FLAGS_1_the_operator_switches_over_the_route_and_the_file_changes()
    {
        var (dir, router) = Rig(false);
        var file = Path.Combine(dir, "kit.beh");

        Assert.Equal(200, Set(router, "FEAT-A", "testers", true).Status);

        Assert.Contains("# kit:switch FEAT-A testers on", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Empty(Served(router).Off.Where(id => id == "FEAT-A"));

        Assert.Equal(200, Set(router, "FEAT-A", "testers", true).Status);
        Assert.Equal(1, File.ReadAllText(file).Split("kit:switch").Length - 1);

        Assert.Equal(200, Set(router, "FEAT-A", "testers", false).Status);
        Assert.Equal(Plain, File.ReadAllText(file));
    }

    [Fact]
    public void BEH_FLAGS_1_a_signed_in_tester_cannot_write_a_switch_and_nothing_is_written()
    {
        var (dir, router) = Rig(true);
        var ann = SignIn(router, "ann", AnnPassword);

        var refused = router.Route("POST", "/api/settings/features", ann, body: Json(new { feature = "FEAT-A", group = "testers", on = true }));

        Assert.Equal(403, refused.Status);
        Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));

        var him = SignIn(router, null, Operator);
        Assert.Equal(200, Set(router, "FEAT-A", "testers", true, him).Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"feature\":\"FEAT-A\",\"group\":\"testers\"}")]
    [InlineData("{\"feature\":\"FEAT-A\",\"group\":\"testers\",\"on\":\"yes\"}")]
    [InlineData("{\"feature\":\"FEAT-A\",\"group\":7,\"on\":true}")]
    public void BEH_FLAGS_1_a_bad_body_is_a_400_and_nothing_is_written(string body)
    {
        var (dir, router) = Rig(false);

        var r = router.Route("POST", "/api/settings/features", body: JsonDocument.Parse(body).RootElement);

        Assert.Equal(400, r.Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));
    }

    [Theory]
    [InlineData("FEAT-NOPE", "testers", 404)]
    [InlineData("FEAT-A", "everyone", 409)]
    public void BEH_FLAGS_1_an_unknown_feature_or_group_is_refused_and_nothing_is_written(string feature, string group, int status)
    {
        var (dir, router) = Rig(false);

        Assert.Equal(status, Set(router, feature, group, true).Status);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));
    }
}
