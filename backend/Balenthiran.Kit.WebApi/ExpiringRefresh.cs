namespace Balenthiran.Kit.WebApi;

/// <summary>
/// A refresh that runs only when asked for and the last one started more than <c>maxAge</c> ago
/// (kit#300, BEH-PULL-5). Callers that ask while one is running join it rather than start another.
/// </summary>
/// <remarks>
/// Age is counted from when a refresh STARTED, not when it succeeded: a GitHub outage then costs
/// one attempt per <c>maxAge</c>, not one per request. The refresh runs detached from whoever
/// started it, so a caller that stops waiting, or disconnects, never cancels it for the others.
/// </remarks>
public sealed class ExpiringRefresh(Func<Task> refresh, TimeSpan maxAge, TimeProvider clock)
{
    private readonly Lock gate = new();
    private Task current = Task.CompletedTask;
    private DateTimeOffset? started;

    /// <summary>
    /// The refresh in flight, a new one if the last is older than <c>maxAge</c>, or a completed
    /// task if it is still fresh. Never faults: <c>refresh</c> must say its own failures.
    /// </summary>
    public Task EnsureFresh()
    {
        lock (gate)
        {
            if (!current.IsCompleted)
            {
                return current;
            }

            var now = clock.GetUtcNow();
            if (started is { } s && now - s < maxAge)
            {
                return Task.CompletedTask;
            }

            started = now;
            current = Task.Run(refresh);
            return current;
        }
    }

    /// <summary>
    /// <see cref="EnsureFresh"/>, waited on for at most <paramref name="wait"/>. True when the
    /// cache is fresh on return; false when the caller gave up and the refresh carries on.
    /// </summary>
    public async Task<bool> EnsureFreshAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        var task = EnsureFresh();
        try
        {
            await task.WaitAsync(wait, cancellationToken);
            return true;
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }
}
