using System.Net.Http.Headers;
using System.Text.Json;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Services;

/// <summary>
/// BEH-USERS-9's metered model proxy: <c>POST /api/agent/v1/messages</c>. A run reaches the model only through
/// here, with its run token as the API key, so Claude Code works unchanged with
/// <c>ANTHROPIC_BASE_URL=&lt;kit&gt;/api/agent</c>. Kit looks the run up by the token's SHA-256, refuses what must
/// not spend (unknown token 401, a run that is over 403, a run at its cap 402), forwards the body with Kit's
/// own key, relays the answer as it arrives (a stream is never buffered whole) and adds what it used to the run.
/// Neither the run token nor Kit's key is logged or sent anywhere they should not be.
/// </summary>
/// <param name="runs">Where runs and their spend are.</param>
/// <param name="http">The client to Anthropic; a test hands it a scripted handler so nothing touches the network.</param>
/// <param name="apiKey"><c>KIT_ANTHROPIC_API_KEY</c>, or null: the proxy then answers 503.</param>
/// <param name="capUsd">What one run may spend (<c>KIT_AGENT_RUN_CAP_USD</c>).</param>
/// <param name="clock">Reads the time a run's age is measured against; null is the system clock.</param>
/// <param name="maxAge">How long after its creation a run's token reaches the model (<c>KIT_AGENT_RUN_MAX_HOURS</c>); null is <see cref="DefaultMaxAge"/>.</param>
public sealed class AgentProxy(IAgentRunStore runs, HttpClient http, string? apiKey, decimal capUsd = AgentProxy.DefaultCapUsd, TimeProvider? clock = null, TimeSpan? maxAge = null)
{
    /// <summary>What one run may spend when <c>KIT_AGENT_RUN_CAP_USD</c> is unset: my assumption, his to change.</summary>
    public const decimal DefaultCapUsd = 5m;

    /// <summary>How long a run's token works when <c>KIT_AGENT_RUN_MAX_HOURS</c> is unset: my assumption, his to change.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(6);

    private readonly TimeSpan ageLimit = maxAge ?? DefaultMaxAge;
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    /// <summary>The largest request body relayed, in bytes: an agent's whole context goes in each call.</summary>
    public const int MaxBody = 32 * 1024 * 1024;

    private const string DefaultVersion = "2023-06-01";

    private static readonly Uri Messages = new("https://api.anthropic.com/v1/messages");
    private static readonly Uri CountTokens = new("https://api.anthropic.com/v1/messages/count_tokens");

    /// <summary>
    /// Answers one call. <paramref name="begin"/> is told the status and content type once they are known and
    /// returns the stream to write the body to, so a streamed answer is written, and flushed, as it arrives.
    /// </summary>
    public async Task ServeAsync(AgentCall call, Func<int, string, Stream> begin, CancellationToken cancellationToken = default)
    {
        if (Check(call.RunToken, call.CountTokens, out var found) is { } refused)
        {
            await RefuseAsync(begin, refused.Status, refused.Type, refused.Message, cancellationToken);
            return;
        }

        var run = found!;

        if (call.Body is null)
        {
            await RefuseAsync(begin, 413, "request_too_large", $"a request body over {MaxBody / (1024 * 1024)} MB is not relayed", cancellationToken);
            return;
        }

        string? model;
        bool stream;
        try
        {
            using var doc = JsonDocument.Parse(call.Body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            model = doc.RootElement.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            stream = doc.RootElement.TryGetProperty("stream", out var s) && s.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            await RefuseAsync(begin, 400, "invalid_request_error", "the body must be a JSON object", cancellationToken);
            return;
        }

        if (run.Status == AgentRunStatus.Queued && !call.CountTokens)
        {
            runs.SetStatus(run.Id, AgentRunStatus.Running);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, call.CountTokens ? CountTokens : Messages)
        {
            Content = new ByteArrayContent(call.Body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // Kit's key, never the caller's token; the caller's version and beta flags are theirs to choose.
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", string.IsNullOrWhiteSpace(call.AnthropicVersion) ? DefaultVersion : call.AnthropicVersion);
        if (!string.IsNullOrWhiteSpace(call.AnthropicBeta))
        {
            request.Headers.TryAddWithoutValidation("anthropic-beta", call.AnthropicBeta);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The type only: nothing from the request goes in a log line.
            await Console.Error.WriteLineAsync($"kit: an agent run's model call failed — {e.GetType().Name}");
            await RefuseAsync(begin, 502, "api_error", "the model could not be reached", cancellationToken);
            return;
        }

        using (response)
        {
            var tally = new UsageTally();
            var ok = response.IsSuccessStatusCode;
            var output = begin((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "application/json");
            try
            {
                if (ok && stream && !call.CountTokens)
                {
                    await RelayStreamAsync(response, output, new SseUsageReader(tally), cancellationToken);
                }
                else
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (ok && !call.CountTokens)
                    {
                        ReadUsage(bytes, tally);
                    }

                    await output.WriteAsync(bytes, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
            }
            catch (Exception e) when (e is IOException or HttpRequestException or OperationCanceledException)
            {
                // The stream broke or the caller hung up part-way: it was still paid for, up to what was seen.
            }
            finally
            {
                if (!call.CountTokens && tally.Any)
                {
                    Record(run, model, tally);
                }
            }
        }
    }

    /// <summary>
    /// Writes the refusal a token earns, if any, and says whether it did. The host calls this BEFORE it reads
    /// the body, so a caller with no good token can never make Kit buffer 32 MB.
    /// </summary>
    public async Task<bool> RefuseEarlyAsync(string? runToken, bool countTokens, Func<int, string, Stream> begin, CancellationToken cancellationToken = default)
    {
        if (Check(runToken, countTokens, out _) is not { } refused)
        {
            return false;
        }

        await RefuseAsync(begin, refused.Status, refused.Type, refused.Message, cancellationToken);
        return true;
    }

    /// <summary>What a call is refused with: its status and Anthropic's error type and message.</summary>
    private sealed record Refusal(int Status, string Type, string Message);

    /// <summary>The checks that need no body, in order: unknown 401, over or expired 403, no key 503, at the cap 402.</summary>
    private Refusal? Check(string? runToken, bool countTokens, out IAgentRun? run)
    {
        run = runToken is { Length: > 0 } token ? runs.ByTokenHash(AgentTokens.Hash(token)) : null;
        if (run is null)
        {
            return new Refusal(401, "authentication_error", "this is not a run token Kit knows");
        }

        if (AgentRunStatus.IsOver(run.Status))
        {
            return new Refusal(403, "permission_error", $"this run is {run.Status}, so its token no longer reaches the model");
        }

        if (time.GetUtcNow() - run.Started > ageLimit)
        {
            return new Refusal(403, "permission_error", $"this run is more than {ageLimit.TotalHours:0.##} hours old, so its token no longer reaches the model");
        }

        if (apiKey is null)
        {
            return new Refusal(503, "api_error", "Kit has no Anthropic key (KIT_ANTHROPIC_API_KEY), so it cannot reach the model");
        }

        // The cap is on what the run HAS spent: calls already in flight can carry it a little past.
        if (!countTokens && run.CostUsd >= capUsd)
        {
            return new Refusal(402, "billing_error", $"this run has spent ${run.CostUsd:0.####} and its cap is ${capUsd:0.####}");
        }

        return null;
    }

    /// <summary>Copies the stream to the caller one chunk at a time, flushing each, so events arrive when Anthropic sends them.</summary>
    private static async Task RelayStreamAsync(HttpResponseMessage response, Stream output, SseUsageReader reader, CancellationToken cancellationToken)
    {
        await using var upstream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[16 * 1024];
        int n;
        while ((n = await upstream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            reader.Feed(buffer.AsSpan(0, n));
            await output.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }

    private static void ReadUsage(byte[] json, UsageTally tally)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("usage", out var usage))
            {
                tally.Apply(usage);
            }
        }
        catch (JsonException)
        {
            // An answer that is not JSON has no usage to read.
        }
    }

    private void Record(IAgentRun run, string? model, UsageTally t)
    {
        var cost = AgentPrices.Cost(model, t.Input, t.Output, t.Write5m, t.Write1h, t.Read);
        runs.AddUsage(run.Id, new AgentUsage(t.Input, t.Output, t.Write5m + t.Write1h, t.Read), cost);
    }

    private static async Task RefuseAsync(Func<int, string, Stream> begin, int status, string type, string message, CancellationToken cancellationToken)
    {
        // Anthropic's own error shape, which Claude Code knows how to show.
        var body = JsonSerializer.SerializeToUtf8Bytes(new { type = "error", error = new { type, message } });
        var output = begin(status, "application/json");
        await output.WriteAsync(body, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
