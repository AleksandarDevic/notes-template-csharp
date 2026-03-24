using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.AI;

internal sealed class NotePolisherTools(IApplicationDbContext dbContext)
{
    public async Task<object> GetNoteContentAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .Select(n => new { n.Title, n.ContentRaw, Category = n.Category.ToString() })
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return new { error = $"Note {noteId} not found." };

        return note;
    }

    public async Task<object> GetSimilarNotesAsync(Guid noteId, int count = 3, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .Select(n => n.Category)
            .FirstOrDefaultAsync(cancellationToken);

        var similar = await dbContext.Notes
            .Where(n => n.Id != noteId && n.Category == category && n.ContentPolished != null)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(count)
            .Select(n => new { n.Title, n.ContentPolished })
            .ToListAsync(cancellationToken);

        return similar;
    }

    public async Task<object> SavePolishedContentAsync(Guid noteId, string polishedContent, CancellationToken cancellationToken = default)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return new { error = $"Note {noteId} not found." };

        note.ContentPolished = polishedContent;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new { success = true };
    }
}
