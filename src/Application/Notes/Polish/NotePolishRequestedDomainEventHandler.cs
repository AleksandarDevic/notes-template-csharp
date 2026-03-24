using Application.Abstractions.AI;
using Application.Abstractions.Data;
using Application.Exceptions;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Polish;

internal sealed class NotePolishRequestedDomainEventHandler(
    IApplicationDbContext dbContext,
    INotePolishOrchestrator orchestrator)
    : IDomainEventHandler<NotePolishRequestedDomainEvent>
{
    public async Task Handle(NotePolishRequestedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var noteId = domainEvent.NoteId;

        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NoteAppException(
                nameof(NotePolishRequestedDomainEventHandler),
                NoteErrors.NotFound(noteId));

        await orchestrator.ExecuteAsync(note.Id, cancellationToken);
    }
}
