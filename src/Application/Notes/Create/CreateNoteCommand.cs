using Application.Abstractions.Messaging;

namespace Application.Notes.Create;

public sealed class CreateNoteCommand : ICommand<Guid>
{
    public required string Title { get; init; }
    public string Content { get; set; } = string.Empty;
}