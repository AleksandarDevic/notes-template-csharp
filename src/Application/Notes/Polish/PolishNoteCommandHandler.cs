using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Polish;

internal sealed class PolishNoteCommandHandler(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PolishNoteCommand>
{
    public async Task<Result> Handle(PolishNoteCommand command, CancellationToken cancellationToken)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == command.NoteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return Result.Failure(NoteErrors.NotFound(command.NoteId));

        if (note.Status != NoteStatus.Draft && note.Status != NoteStatus.AIReady)
            return Result.Failure(NoteErrors.InvalidStatus(note.Id, note.Status));

        note.Status = NoteStatus.ProcessingAI;
        note.UpdatedAtUtc = dateTimeProvider.UtcNow;

        note.Raise(new NotePolishRequestedDomainEvent(note.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
