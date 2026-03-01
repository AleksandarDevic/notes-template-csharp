using Application.Abstractions.AI;
using Domain.Notes;
using Microsoft.Extensions.AI;

namespace Infrastructure.AI;

internal sealed class OllamaService(IChatClient chatClient) : INoteCategoryService
{
    public async Task<NoteCategory> GetCategoryAsync(string noteContent)
    {
        var prompt =
        """
        You are a note categorization assistant.
        Classify the following note into exactly one of these categories:
        Personal, Work, Programming, Learning, Ideas, Finance, Health, Travel, Goals, Other.

        Rules:
        - Respond with ONLY the category name, nothing else.
        - No punctuation, no explanation, no extra text.
        - If unsure, respond with: Other
        """;
        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, prompt),
            new ChatMessage(ChatRole.User, $"Note content:\n{noteContent}")
        ];

        var response = await chatClient.GetResponseAsync(messages);

        if (!response.Messages.Any())
            return NoteCategory.Other;

        return Enum.TryParse<NoteCategory>(
            response.Messages.First().Text.Trim(), ignoreCase: true, out var result) ?
            result :
            NoteCategory.Other;
    }
}
