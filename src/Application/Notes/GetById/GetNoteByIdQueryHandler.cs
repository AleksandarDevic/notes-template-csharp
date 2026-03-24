using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Notes.GetById;

internal sealed class GetNoteByIdQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<GetNoteByIdQuery, NoteResponse>
{
    public async Task<Result<NoteResponse>> Handle(GetNoteByIdQuery query, CancellationToken cancellationToken)
    {
        NoteResponse? @note = await dbContext.Notes
        .Where(n => n.Id == query.NoteId)
        .Select(note => new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            ContentRaw = note.ContentRaw,
            ContentPolished = note.ContentPolished,
            ContentSummary = note.ContentSummary,
            KeyPoints = note.KeyPoints,
            Status = note.Status,
            LastUpdatedAtUtc = note.UpdatedAtUtc
        })
        .FirstOrDefaultAsync(cancellationToken);

        if (@note is null)
            return Result.Failure<NoteResponse>(NoteErrors.NotFound(query.NoteId));

        return @note;
    }
}