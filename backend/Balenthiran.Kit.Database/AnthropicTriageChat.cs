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
    public async Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, bool actions = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = Request(instructions ?? Instructions, context, turns, actions);
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

    /// <summary>
    /// The text blocks of a Messages API answer, joined, and the first tool call it makes (BEH-CHAT-6) as an
    /// action. A call alone is a reply with no words; neither is a failure, not an empty reply. A call to a
    /// tool that is not one of <see cref="Tools"/>, or that lacks the words it needs, is dropped.
    /// </summary>
    public static TriageReply Parse(string json)
    {
        var content = JsonNode.Parse(json)?["content"] as JsonArray;
        var text = string.Concat((content ?? [])
            .Where(b => (string?)b?["type"] == "text")
            .Select(b => (string?)b?["text"] ?? string.Empty)).Trim();
        var action = (content ?? [])
            .Where(b => (string?)b?["type"] == "tool_use")
            .Select(b => ActionOf((string?)b?["name"], b?["input"] as JsonObject))
            .FirstOrDefault(a => a is not null);
        return text.Length > 0 || action is not null
            ? new TriageReply(text, null, action)
            : new TriageReply(null, "the model answered with no text; try rewording");
    }

    private static TriageAction? ActionOf(string? name, JsonObject? input)
    {
        var words = input?["text"] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
        return name switch
        {
            "approve_item" => new TriageAction("approve"),
            "undo_last" => new TriageAction("undo"),
            "deny_item" when words is { Length: > 0 } => new TriageAction("deny", words),
            "answer_item" when words is { Length: > 0 } => new TriageAction("answer", words),
            _ => null,
        };
    }

    /// <summary>
    /// BEH-CHAT-6: what the Chat page's agent may ask to be done. The page carries each one out with the
    /// owner's own writes and offers to undo it; the server writes nothing for them.
    /// </summary>
    public static JsonArray Tools() => new(
        Tool("approve_item", "Approve the item in hand, when the owner has said it is right as it stands.", words: null),
        Tool("deny_item", "Deny the item in hand, when the owner has said it is wrong; text is what is true instead, in their words.", words: "What is true instead."),
        Tool("answer_item", "Post an answer to the item in hand's question, when the owner has given one; text is the answer, in their words.", words: "The answer."),
        Tool("undo_last", "Undo the last thing done to an item in this chat, when the owner asks to.", words: null));

    private static JsonObject Tool(string name, string description, string? words)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        if (words is not null)
        {
            properties["text"] = new JsonObject { ["type"] = "string", ["description"] = words };
            required.Add("text");
        }

        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["input_schema"] = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required },
        };
    }

    private HttpRequestMessage Request(string instructions, string context, IReadOnlyList<TriageTurn> turns, bool actions)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = MaxTokens,
            ["system"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = instructions },
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
        if (actions)
        {
            body["tools"] = Tools();
        }

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
