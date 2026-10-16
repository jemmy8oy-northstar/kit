using Balenthiran.Kit.WebApi;

namespace Balenthiran.Kit.Tests;

/// <summary>
/// kit#300, BEH-PULL-5: "could we not just maintain some in memory cache which expires. So if a
/// request is made it checks if the cache has expired." Scored by how many times the refresh ran.
/// </summary>
public sealed class ExpiringRefreshTests
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(120);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Nothing_is_read_until_a_request_asks()
    {
        var reads = 0;
        var clock = new Clock();
        var r = new ExpiringRefresh(() => { reads++; return Task.CompletedTask; }, MaxAge, clock);

        clock.Now += TimeSpan.FromHours(5);
        Assert.Equal(0, reads);

        Assert.True(await r.EnsureFreshAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task A_fresh_read_is_served_and_an_expired_one_is_read_again()
    {
        var reads = 0;
        var clock = new Clock();
        var r = new ExpiringRefresh(() => { reads++; return Task.CompletedTask; }, MaxAge, clock);

        await r.EnsureFresh();
        clock.Now += MaxAge - TimeSpan.FromSeconds(1);
        await r.EnsureFresh();
        Assert.Equal(1, reads);

        clock.Now += TimeSpan.FromSeconds(1);
        await r.EnsureFresh();
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task Requests_that_arrive_during_a_read_join_it_rather_than_start_another()
    {
        var reads = 0;
        var release = new TaskCompletionSource();
        var clock = new Clock();
        var r = new ExpiringRefresh(async () => { Interlocked.Increment(ref reads); await release.Task; }, MaxAge, clock);

        var first = r.EnsureFresh();

        // Even past the age: a read still in flight is the freshest there is.
        clock.Now += MaxAge * 2;
        var second = r.EnsureFresh();

        Assert.Same(first, second);
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task A_slow_read_lets_the_request_go_after_the_wait_and_carries_on()
    {
        var release = new TaskCompletionSource();
        var done = false;
        var r = new ExpiringRefresh(async () => { await release.Task; done = true; }, MaxAge, new Clock());

        Assert.False(await r.EnsureFreshAsync(TimeSpan.FromMilliseconds(50)));
        Assert.False(done);

        release.SetResult();
        await r.EnsureFresh();
        Assert.True(done);
    }

    [Fact]
    public async Task A_request_that_disconnects_does_not_cancel_the_read_for_the_others()
    {
        var release = new TaskCompletionSource();
        var r = new ExpiringRefresh(() => release.Task, MaxAge, new Clock());
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();

        Assert.False(await r.EnsureFreshAsync(TimeSpan.FromSeconds(3), gone.Token));

        var joined = r.EnsureFresh();
        Assert.False(joined.IsCompleted);
        release.SetResult();
        await joined;
    }

    [Fact]
    public async Task A_failed_read_is_retried_after_the_age_not_on_every_request()
    {
        var reads = 0;
        var clock = new Clock();

        // The real refresh says its own failure and returns; a faulted one must still age out.
        var r = new ExpiringRefresh(() => { reads++; return Task.FromException(new HttpRequestException("down")); }, MaxAge, clock);

        await Assert.ThrowsAsync<HttpRequestException>(() => r.EnsureFresh());
        await r.EnsureFresh();
        Assert.Equal(1, reads);

        clock.Now += MaxAge;
        await Assert.ThrowsAsync<HttpRequestException>(() => r.EnsureFresh());
        Assert.Equal(2, reads);
    }
}
