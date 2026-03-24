using Application.Abstractions.Messaging;

namespace Application.Notes.Approve;

public sealed class ApproveNoteCommand : ICommand
{
    public required Guid NoteId { get; init; }
}
