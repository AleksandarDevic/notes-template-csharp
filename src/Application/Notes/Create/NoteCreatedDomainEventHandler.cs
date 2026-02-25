using Domain.Notes;
using SharedKernel;

namespace Application.Notes.Create;

internal sealed class NoteCreatedDomainEventHandler : IDomainEventHandler<NoteCreatedDomainEvent>
{
    public Task Handle(NoteCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // TODO: Add some logic here
        return Task.CompletedTask;
    }
}