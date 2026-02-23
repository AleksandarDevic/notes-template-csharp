using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.Get;

internal sealed class GetNotesQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetNotesQuery, List<NoteResponse>>
{
    public async Task<Result<List<NoteResponse>>> Handle(GetNotesQuery query, CancellationToken cancellationToken)
    {
        List<NoteResponse> notes = await dbContext.Notes
        .Select(note => new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            ContentRaw = note.ContentRaw,
            ContentPolished = note.ContentPolished,
            Status = note.Status,
            LastUpdatedAtUtc = note.UpdatedAtUtc
        })
        .ToListAsync(cancellationToken);

        return notes;
    }
}