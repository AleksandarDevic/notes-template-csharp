using Application.Abstractions.Messaging;

namespace Application.Notes.Update;

public sealed class UpdateNoteCommand : ICommand
{
    public required Guid NoteId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
}
