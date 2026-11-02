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

    /// <summary>The most rounds of reading the spec one reply may take before it must answer: each is a request paid for.</summary>
    public const int MaxReads = 2;

    /// <summary>The most cards one read asks for.</summary>
    public const int MaxReadIds = 10;

    private const string ReadTool = "read_spec";

    private static readonly Uri Endpoint = new("https://api.anthropic.com/v1/messages");

    private const string Instructions =
        "You are Kit's spec agent. You talk one card from Kit's Inbox through with its owner, who must decide it: "
        + "approve it, deny it with what is true instead, or answer its question. You write no code and change nothing; "
        + "the owner copies what you agree into their answer. Be brief and plain: a few sentences, no headings. "
        + "Say what the card is asking and what each choice would mean for the project, using the spec below. "
        + "When the owner seems to have decided, offer the answer in one or two sentences they could post as it stands. "
        + "The project's spec is given by title: read whole cards with read_spec when a title is not enough. "
        + "If the spec does not say, say so rather than guess.";

    /// <inheritdoc />
    public string Model => model;

    /// <inheritdoc />
    public async Task<TriageReply> ReplyAsync(string context, IReadOnlyList<TriageTurn> turns, string? instructions = null, bool actions = false, Func<IReadOnlyList<string>, string>? read = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new JsonArray(turns
                .Select(t => (JsonNode)new JsonObject { ["role"] = t.FromUser ? "user" : "assistant", ["content"] = t.Text })
                .ToArray());
            for (var round = 0; ; round++)
            {
                // The last round still defines the tools, as the history holds calls to them, but lets none be called,
                // so it answers in words. Not where it may act: that would forbid the action it read up for.
                using var request = Request(instructions ?? Instructions, context, messages, actions, reading: read is not null, last: read is not null && !actions && round >= MaxReads);
                using var response = await http.SendAsync(request, cancellationToken);
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // The status only: an error body can echo the request, and the request is his spec.
                    await Console.Error.WriteLineAsync($"kit: the Anthropic API answered {(int)response.StatusCode} to a triage turn");
                    return new TriageReply(null, $"the model's API answered {(int)response.StatusCode}, so there is no reply; try again in a minute");
                }

                // Past the last round a further read is not done: the reply is whatever words came with it.
                if (read is null || round >= MaxReads || Reads(text) is not { Count: > 0 } asked)
                {
                    return Parse(text);
                }

                // His 18:55Z note: the agent reads the cards it asked for, then is asked again.
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = JsonNode.Parse(text)!["content"]!.DeepClone() });
                messages.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(asked
                        .Select(a => (JsonNode)new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = a.Id, ["content"] = read!(a.Ids) })
                        .ToArray()),
                });
            }
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
        var calls = (content ?? [])
            .Where(b => (string?)b?["type"] == "tool_use" && ActionTools.Contains((string?)b?["name"] ?? string.Empty))
            .ToList();
        var action = calls
            .Select(b => ActionOf((string?)b?["name"], b?["input"] as JsonObject))
            .FirstOrDefault(a => a is not null);
        if (text.Length == 0 && action is null)
        {
            return new TriageReply(null, "the model answered with no text; try rewording");
        }

        // An action asked for and not done (a second one, or one without its words) is said, so the agent's
        // words never stand alone claiming it.
        var dropped = calls.Count - (action is null ? 0 : 1);
        return new TriageReply(dropped > 0 ? $"{text}\n\n{Dropped(dropped)}".TrimStart() : text, null, action);
    }

    /// <summary>What Kit adds to a reply when <paramref name="count"/> of the actions it asked for were not done.</summary>
    public static string Dropped(int count) =>
        $"(Kit: {(count == 1 ? "one action" : $"{count} actions")} asked for here {(count == 1 ? "was" : "were")} not done: one is done per reply, and each needs its words.)";

    private static readonly HashSet<string> ActionTools = Tools().Select(t => (string)t!["name"]!).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The cards a reply asks to read, each call with its id; null when it reads nothing, or when it also
    /// asks for an action, which then ends the turn as it stands (every call would need its own result).
    /// </summary>
    public static IReadOnlyList<(string Id, IReadOnlyList<string> Ids)>? Reads(string json)
    {
        var calls = ((JsonNode.Parse(json)?["content"] as JsonArray) ?? [])
            .Where(b => (string?)b?["type"] == "tool_use")
            .ToList();
        if (calls.Count == 0 || calls.Any(c => (string?)c?["name"] != ReadTool))
        {
            return null;
        }

        return calls
            .Select(c => (
                (string?)c?["id"] ?? string.Empty,
                (IReadOnlyList<string>)((c?["input"]?["ids"] as JsonArray) ?? [])
                    .Select(i => i is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty)
                    .Where(s => s.Length > 0)
                    .Take(MaxReadIds)
                    .ToList()))
            .ToList();
    }

    private static TriageAction? ActionOf(string? name, JsonObject? input)
    {
        var words = input?["text"] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
        return name switch
        {
            "approve_item" => new TriageAction("approve"),
            "undo_last" => new TriageAction("undo"),
            "show_item" => new TriageAction("show"),
            "deny_item" when words is { Length: > 0 } => new TriageAction("deny", words),
            "answer_item" when words is { Length: > 0 } => new TriageAction("answer", words),
            "save_note" when words is { Length: > 0 } => new TriageAction("note", words),
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
        Tool("undo_last", "Undo the last thing done to an item in this chat, when the owner asks to.", words: null),
        Tool("show_item", "Show the item in hand in the chat again, when the owner asks to see it.", words: null),
        Tool("save_note", "Leave a note on the project for the coding agent, when the owner agrees to save what this chat settled; text is the note: who does what and what they see, in their words, without the chat's back and forth.", words: "The note."));

    /// <summary>His 18:55Z note: the tool that reads whole cards of the project's spec, by id.</summary>
    public static JsonObject ReadSpecTool() => new()
    {
        ["name"] = ReadTool,
        ["description"] = "Read whole cards of this project's spec (an epic, feature or behaviour, with every contract) by id, from the titles you were given, before you answer about them.",
        ["input_schema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["ids"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = $"Up to {MaxReadIds} ids, such as BEH-CHAT-2 or FEAT-CONFLICTS." },
            },
            ["required"] = new JsonArray("ids"),
        },
    };

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

    private HttpRequestMessage Request(string instructions, string context, JsonArray messages, bool actions, bool reading, bool last)
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
            ["messages"] = messages.DeepClone(),
        };
        var tools = actions ? Tools() : [];
        if (reading)
        {
            tools.Add(ReadSpecTool());
        }

        if (tools.Count > 0)
        {
            body["tools"] = tools;
            if (last)
            {
                body["tool_choice"] = new JsonObject { ["type"] = "none" };
            }
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
