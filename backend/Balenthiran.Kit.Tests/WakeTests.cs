using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Database;
using Balenthiran.Kit.DataModels.Models;
using Balenthiran.Kit.Services;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// <c># kit:wake &lt;iso&gt;</c> (BEH-SETTINGS-7): when the operator last asked the agent to wake — read
/// from Kit's own primary file, written by the operator alone, in the exact shape the scheduler greps.
/// </summary>
public class WakeTests
{
    private const string Plain = "# kit:repo o/r@dev\n\nbehaviour BEH-1 \"a thing\"\n  when opens page:Home\n";
    private const string Operator = "operator-pw";
    private const string AnnPassword = "ann-first-password";
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 2, 15, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset At { get; set; } = at;

        public override DateTimeOffset GetUtcNow() => At;
    }

    private static (string Dir, KitRouter Router, FixedClock Clock) Rig(bool accounts, string text = Plain)
    {
        var dir = Directory.CreateTempSubdirectory("kit-wake-").FullName;
        File.WriteAllText(Path.Combine(dir, "kit.beh"), text);

        var corpora = new CorpusDirectory(dir, RepoLayout.Root);
        var viewer = new ProjectViewer(corpora, new CorpusParser(), new BehaviourResolver(), new TestGenerator(), new ProjectReporter());
        var users = new AccountsTests.MemoryUsers();
        users.Add("ann", PasswordHash.Hash(AnnPassword, iterations: 1000));
        var clock = new FixedClock(Now);
        var router = new KitRouter(
            corpora, viewer, new UiBundle(Path.Combine(dir, "no-bundle")), accounts ? Operator : null,
            new OriginPolicy(new UrlParser(), null), new SessionStore(), new SignInThrottle(), users: accounts ? users : null, clock: clock);
        return (dir, router, clock);
    }

    private static string? RequestedOf(KitRouter router, string? cookie = null)
    {
        var r = router.Route("GET", "/api/settings/wake", cookie);
        Assert.Equal(200, r.Status);
        return Assert.IsType<WakeSetting>(r.Body).Requested;
    }

    private static string SignIn(KitRouter router, string? name, string password)
    {
        var body = JsonSerializer.SerializeToElement(name is null ? new { password } : (object)new { username = name, password });
        var r = router.Route("POST", "/api/session", body: body);
        Assert.Equal(200, r.Status);
        return r.SetCookie!.Split(';')[0];
    }

    [Fact]
    public void BEH_SETTINGS_7_no_line_means_requested_is_null()
    {
        var (_, router, _) = Rig(false);

        Assert.Null(RequestedOf(router));
        var json = JsonNode.Parse(new EngineJsonSerialiser().Serialise(router.Route("GET", "/api/settings/wake").Body!))!;
        Assert.True(json.AsObject().ContainsKey("requested"));
        Assert.Null(json["requested"]);
    }

    [Fact]
    public void BEH_SETTINGS_7_post_writes_the_line_with_the_clocks_time()
    {
        var (dir, router, _) = Rig(false);

        Assert.Equal(200, router.Route("POST", "/api/settings/wake").Status);

        var text = File.ReadAllText(Path.Combine(dir, "kit.beh"));
        Assert.Contains("\n# kit:wake 2026-11-07T02:15:00Z\n", text, StringComparison.Ordinal);
        Assert.Equal("2026-11-07T02:15:00Z", RequestedOf(router));
    }

    [Fact]
    public void BEH_SETTINGS_7_a_second_post_replaces_the_line_rather_than_adding_one()
    {
        var (dir, router, clock) = Rig(false);
        router.Route("POST", "/api/settings/wake");

        clock.At = Now.AddHours(3);
        Assert.Equal(200, router.Route("POST", "/api/settings/wake").Status);

        var text = File.ReadAllText(Path.Combine(dir, "kit.beh"));
        Assert.Equal(1, text.Split("kit:wake").Length - 1);
        Assert.Contains("# kit:wake 2026-11-07T05:15:00Z\n", text, StringComparison.Ordinal);
        Assert.Equal("2026-11-07T05:15:00Z", RequestedOf(router));
    }

    [Fact]
    public void BEH_SETTINGS_7_the_writer_drops_repeats_and_touches_nothing_else()
    {
        var messy = "# kit:repo o/r@dev\n# kit:wake nonsense\n# kit:idle ideas\n# kit:wake 2026-01-01T00:00:00Z\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n";

        var r = new CorpusWriter(new CorpusParser()).SetWake(messy, Now);

        Assert.True(r.Ok);
        Assert.Equal("# kit:repo o/r@dev\n# kit:wake 2026-11-07T02:15:00Z\n# kit:idle ideas\nbehaviour BEH-1 \"a\"\n  when opens page:Home\n", r.Text);
    }

    [Fact]
    public void BEH_SETTINGS_7_a_non_operator_post_is_refused_and_nothing_is_written()
    {
        var (dir, router, _) = Rig(true);
        var ann = SignIn(router, "ann", AnnPassword);

        var refused = router.Route("POST", "/api/settings/wake", ann);

        Assert.Equal(403, refused.Status);
        Assert.Equal("not-yours", Assert.IsType<ApiError>(refused.Body).Error);
        Assert.Equal(Plain, File.ReadAllText(Path.Combine(dir, "kit.beh")));

        var him = SignIn(router, null, Operator);
        Assert.Equal(200, router.Route("POST", "/api/settings/wake", him).Status);
        Assert.Equal("2026-11-07T02:15:00Z", RequestedOf(router, him));
    }

    [Fact]
    public void BEH_SETTINGS_7_get_reads_a_written_line()
    {
        var (_, router, _) = Rig(false, "# kit:wake 2026-10-01T09:30:00Z\nbehaviour BEH-1 \"a\"\n");

        Assert.Equal("2026-10-01T09:30:00Z", RequestedOf(router));
    }

    [Theory]
    [InlineData("# kit:wake\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:wake soon\nbehaviour BEH-1 \"a\"\n")]
    [InlineData("# kit:wakeful 2026-10-01T09:30:00Z\nbehaviour BEH-1 \"a\"\n")]
    public void BEH_SETTINGS_7_a_malformed_line_reads_as_never_asked(string text)
    {
        Assert.Null(WakeDirective.Read(text));
    }

    [Fact]
    public void BEH_SETTINGS_7_the_written_line_is_what_the_scheduler_greps()
    {
        var (dir, router, _) = Rig(false);
        router.Route("POST", "/api/settings/wake");

        var lines = File.ReadAllLines(Path.Combine(dir, "kit.beh")).Where(l => Regex.IsMatch(l, "^# kit:wake [0-9]")).ToList();

        var line = Assert.Single(lines);
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, fields.Length);
        Assert.True(DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at));
        Assert.Equal(Now, at);
        Assert.EndsWith("Z", fields[2], StringComparison.Ordinal);
    }
}
