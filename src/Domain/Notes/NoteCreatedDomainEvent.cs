using SharedKernel;

namespace Domain.Notes;

public sealed class NoteCreatedDomainEvent(Guid NoteId) : IDomainEvent
{
    public Guid NoteId { get; init; } = NoteId;
}