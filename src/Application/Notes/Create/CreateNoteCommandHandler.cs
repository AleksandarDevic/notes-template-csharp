using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Notes;
using SharedKernel;

namespace Application.Notes.Create;

internal sealed class CreateNoteCommandHandler(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateNoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateNoteCommand command, CancellationToken cancellationToken)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            ContentRaw = command.Content,
            Status = NoteStatus.Draft,
            CreatedAtUtc = dateTimeProvider.UtcNow
        };

        note.Raise(new NoteCreatedDomainEvent(note.Id));

        dbContext.Notes.Add(note);

        await dbContext.SaveChangesAsync(cancellationToken);

        return note.Id;
    }
}