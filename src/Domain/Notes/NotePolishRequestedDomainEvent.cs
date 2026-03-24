using SharedKernel;

namespace Domain.Notes;

public sealed class NotePolishRequestedDomainEvent(Guid NoteId) : IDomainEvent
{
    public Guid NoteId { get; init; } = NoteId;
}
