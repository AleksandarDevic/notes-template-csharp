using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Update;

internal sealed class UpdateNoteCommandHandler(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateNoteCommand>
{
    public async Task<Result> Handle(UpdateNoteCommand command, CancellationToken cancellationToken)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == command.NoteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return Result.Failure(NoteErrors.NotFound(command.NoteId));

        if (note.Status == NoteStatus.ProcessingAI)
            return Result.Failure(NoteErrors.InvalidStatus(note.Id, note.Status));

        note.Title = command.Title;
        note.ContentRaw = command.Content;
        note.UpdatedAtUtc = dateTimeProvider.UtcNow;

        // If AI already ran, clear results — ContentRaw changed so they are no longer valid
        if (note.Status == NoteStatus.AIReady || note.Status == NoteStatus.Completed)
        {
            note.Status = NoteStatus.Draft;
            note.ContentPolished = null;
            note.ContentSummary = null;
            note.KeyPoints = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
