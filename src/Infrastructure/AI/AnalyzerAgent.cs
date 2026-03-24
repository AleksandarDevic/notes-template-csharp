using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace Infrastructure.AI;

internal sealed class AnalyzerAgent(
    NoteAnalyzerTools tools,
    IOptions<AnthropicOptions> options)
    : IAnalyzerAgent
{
    private const string SystemPrompt =
        """
        You are a text analyst. Your task is to analyze a polished note and extract:
        - A concise summary (2-3 sentences)
        - Key points as a JSON array of strings

        Use the available tools to read the note, then save the results using SaveAnalysis.

        Always:
        1. Call GetPolishedContent to read the polished note.
        2. Optionally call GetNoteHistory to compare raw vs polished.
        3. Analyze and extract summary and key points.
        4. Call SaveAnalysis with the results.
        """;

    private readonly List<ToolUnion> _tools =
    [
        new Tool
        {
            Name = "GetPolishedContent",
            Description = "Retrieve the polished content of the note.",
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
            Name = "GetNoteHistory",
            Description = "Retrieve both raw and polished content to see what changed.",
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
            Name = "SaveAnalysis",
            Description = "Save the summary and key points. Sets note status to AIReady.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["noteId"] = JsonSerializer.SerializeToElement(new { type = "string", description = "The note ID (GUID)." }),
                    ["summary"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Concise 2-3 sentence summary." }),
                    ["keyPoints"] = JsonSerializer.SerializeToElement(new { type = "string", description = "JSON array of key point strings." })
                },
                Required = ["noteId", "summary", "keyPoints"]
            }
        }
    ];

    public async Task RunAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var client = new AnthropicClient { ApiKey = options.Value.ApiKey };

        var messages = new List<MessageParam>
        {
            new() { Role = Role.User, Content = $"Analyze the note with ID: {noteId}" }
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
            "GetPolishedContent" => await tools.GetPolishedContentAsync(
                ParseGuid(toolUse.Input, "noteId", noteId)),

            "GetNoteHistory" => await tools.GetNoteHistoryAsync(
                ParseGuid(toolUse.Input, "noteId", noteId)),

            "SaveAnalysis" => await tools.SaveAnalysisAsync(
                ParseGuid(toolUse.Input, "noteId", noteId),
                toolUse.Input["summary"].GetString()!,
                toolUse.Input["keyPoints"].GetString()!),

            _ => new { error = $"Unknown tool: {toolUse.Name}" }
        };
    }

    private static Guid ParseGuid(IReadOnlyDictionary<string, JsonElement> input, string key, Guid fallback) =>
        input.TryGetValue(key, out var el) && Guid.TryParse(el.GetString(), out var id) ? id : fallback;
}
