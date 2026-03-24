using Application.Abstractions.Messaging;

namespace Application.Notes.Reject;

public sealed class RejectNoteCommand : ICommand
{
    public required Guid NoteId { get; init; }
}
