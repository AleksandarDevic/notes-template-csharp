using Application.Abstractions.Messaging;

namespace Application.Notes.Polish;

public sealed class PolishNoteCommand : ICommand
{
    public required Guid NoteId { get; init; }
}
