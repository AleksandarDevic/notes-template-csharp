using Application.Abstractions.AI;
using Application.Abstractions.Data;
using Application.Exceptions;
using Application.Notes.GetById;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Create;

internal sealed class NoteCreatedDomainEventHandler(
    IApplicationDbContext dbContext,
    INoteCategoryService categoryService)
    : IDomainEventHandler<NoteCreatedDomainEvent>
{
    public async Task Handle(NoteCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var noteId = domainEvent.NoteId;
        var note = await dbContext.Notes.Where(n => n.Id == noteId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NoteAppException(nameof(NoteCreatedDomainEventHandler), NoteErrors.NotFound(noteId));

        var category = await categoryService.GetCategoryAsync(note.ContentRaw);
        note.Category = category;

        dbContext.Notes.Update(note);

        await dbContext.SaveChangesAsync(cancellationToken);

    }
}