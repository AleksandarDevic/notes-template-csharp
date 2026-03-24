using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Approve;

internal sealed class ApproveNoteCommandHandler(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ApproveNoteCommand>
{
    public async Task<Result> Handle(ApproveNoteCommand command, CancellationToken cancellationToken)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == command.NoteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return Result.Failure(NoteErrors.NotFound(command.NoteId));

        if (note.Status != NoteStatus.AIReady)
            return Result.Failure(NoteErrors.InvalidStatus(note.Id, note.Status));

        note.Status = NoteStatus.Completed;
        note.UpdatedAtUtc = dateTimeProvider.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
