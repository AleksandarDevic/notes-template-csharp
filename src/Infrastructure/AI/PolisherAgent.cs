using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace Infrastructure.AI;

internal sealed class PolisherAgent(
    NotePolisherTools tools,
    IOptions<AnthropicOptions> options)
    : IPolisherAgent
{
    private const string SystemPrompt =
        """
        You are a professional writer. Your task is to polish a note's raw content into
        well-structured, clear, and engaging prose while preserving the original meaning.

        Use the available tools to gather information about the note and similar notes,
        then save the polished version using SavePolishedContent.

        Always:
        1. Call GetNoteContent first to read the note.
        2. Optionally call GetSimilarNotes to match the user's writing style.
        3. Polish the content.
        4. Call SavePolishedContent with the result.
        """;

    private readonly List<ToolUnion> _tools =
    [
        new Tool
        {
            Name = "GetNoteContent",
            Description = "Retrieve the note's title, raw content and category.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["noteId"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note ID (GUID)." })
                },
                Required = ["noteId"]
            }
        },
        new Tool
        {
            Name = "GetSimilarNotes",
            Description = "Retrieve the last N polished notes of the same category to match writing style.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["noteId"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note ID (GUID)." }),
                    ["count"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Number of similar notes to retrieve (default 3)." })
                },
                Required = ["noteId"]
            }
        },
        new Tool
        {
            Name = "SavePolishedContent",
            Description = "Save the polished version of the note content.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["noteId"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note ID (GUID)." }),
                    ["polishedContent"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The polished note content." })
                },
                Required = ["noteId", "polishedContent"]
            }
        }
    ];

    public async Task RunAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var client = new AnthropicClient { ApiKey = options.Value.ApiKey };

        var messages = new List<MessageParam>
        {
            new() { Role = Role.User, Content = $"Polish the note with ID: {noteId}" }
        };

        while (true)
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = options.Value.Model,
                MaxTokens = 4096,
                System = SystemPrompt,
                Tools = _tools,
                Messages = messages
            }, cancellationToken: cancellationToken);

            // Convert IReadOnlyList<ContentBlock> → List<ContentBlockParam> via JSON round-trip
            // (same wire format, different C# types for request vs response)
            var assistantContent = JsonSerializer.Deserialize<List<ContentBlockParam>>(
                JsonSerializer.Serialize(response.Content))!;
            messages.Add(new() { Role = Role.Assistant, Content = assistantContent });

            if (response.StopReason == StopReason.EndTurn)
                break;

            if (response.StopReason != StopReason.ToolUse)
                break;

            var toolResults = new List<ContentBlockParam>();

            foreach (var block in response.Content)
            {
                if (!block.TryPickToolUse(out var toolUse))
                    continue;

                var result = await ExecuteToolAsync(toolUse, noteId, cancellationToken);

                toolResults.Add(new ToolResultBlockParam
                {
                    ToolUseID = toolUse.ID,
                    Content = JsonSerializer.Serialize(result)
                });
            }

            messages.Add(new() { Role = Role.User, Content = toolResults });
        }
    }

    private async Task<object> ExecuteToolAsync(ToolUseBlock toolUse, Guid noteId, CancellationToken cancellationToken)
    {
        return toolUse.Name switch
        {
            "GetNoteContent" => await tools.GetNoteContentAsync(
                ParseGuid(toolUse.Input, "noteId", noteId),
                cancellationToken),

            "GetSimilarNotes" => await tools.GetSimilarNotesAsync(
                ParseGuid(toolUse.Input, "noteId", noteId),
                toolUse.Input.TryGetValue("count", out var c) ? c.GetInt32() : 3,
                cancellationToken),

            "SavePolishedContent" => await tools.SavePolishedContentAsync(
                ParseGuid(toolUse.Input, "noteId", noteId),
                toolUse.Input["polishedContent"].GetString()!,
                cancellationToken: cancellationToken),

            _ => new { error = $"Unknown tool: {toolUse.Name}" }
        };
    }

    private static Guid ParseGuid(IReadOnlyDictionary<string, JsonElement> input, string key, Guid fallback) =>
        input.TryGetValue(key, out var el) && Guid.TryParse(el.GetString(), out var id) ? id : fallback;

}
