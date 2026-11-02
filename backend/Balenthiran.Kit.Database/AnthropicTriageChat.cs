using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Balenthiran.Kit.Abstractions.DataModels;
using Balenthiran.Kit.Abstractions.Services;

namespace Balenthiran.Kit.Database;

/// <summary>
/// <see cref="ITriageChat"/> on Anthropic's Messages API (BEH-SPECAGENT-4), Haiku by his choice
/// (kit.notes.md 2026-11-02 12:04Z). No package: one POST. The card and its project go in the
/// system prompt, marked for caching, so each turn after the first re-reads them at the cached rate.
/// </summary>
public sealed class AnthropicTriageChat(HttpClient http, string apiKey, string model = AnthropicTriageChat.DefaultModel) : ITriageChat
{
    /// <summary>His choice: "Let's use haiku for this".</summary>
    public const string DefaultModel = "claude-haiku-5-5";

    /// <summary>The longest reply, in tokens: a chat answer, not an essay, and the cap on what one turn costs.</summary>
    public const int MaxTokens = 1024;

    private static readonly Uri Endpoint = new("https://api.anthropic.com/v1/messages");

    private const string Instructions =
        "You are Kit's spec agent. You talk one card from Kit's Inbox through with its owner, who must decide it: "
        + "approve it, deny it with what is true instead, or answer its question. You write no code and change nothing; "
        + "the owner copies what you agree into their answer. Be brief and plain: a few sentences, no headings. "
        + "Say what the card is asking and what each choice would mean for the project, using the spec below. "
        + "When the owner seems to have decided, offer the answer in one or two sentences they could post as it stands. "
        + "If the spec does not say, say so rather than guess.";

    /// <inheritdoc />
    public string Model => model;

    /// <inheritdoc />
    public async Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = Request(context, turns);
            using var response = await http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // The status only: an error body can echo the request, and the request is his spec.
                await Console.Error.WriteLineAsync($"kit: the Anthropic API answered {(int)response.StatusCode} to a triage turn");
                return new TriageReply(null, $"the model's API answered {(int)response.StatusCode}, so there is no reply; try again in a minute");
            }

            return Parse(text);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            await Console.Error.WriteLineAsync($"kit: a triage turn failed — {e.GetType().Name}");
            return new TriageReply(null, "the model could not be reached, so there is no reply; try again in a minute");
        }
    }

    /// <summary>The text blocks of a Messages API answer, joined; none is a failure, not an empty reply.</summary>
    public static TriageReply Parse(string json)
    {
        var content = JsonNode.Parse(json)?["content"] as JsonArray;
        var text = string.Concat((content ?? [])
            .Where(b => (string?)b?["type"] == "text")
            .Select(b => (string?)b?["text"] ?? string.Empty)).Trim();
        return text.Length > 0 ? new TriageReply(text) : new TriageReply(null, "the model answered with no text; try rewording");
    }

    private HttpRequestMessage Request(string context, IReadOnlyList<TriageTurn> turns)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = MaxTokens,
            ["system"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = Instructions },
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = context,
                    ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
                }),
            ["messages"] = new JsonArray(turns
                .Select(t => (JsonNode)new JsonObject { ["role"] = t.FromUser ? "user" : "assistant", ["content"] = t.Text })
                .ToArray()),
        };

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }
}
