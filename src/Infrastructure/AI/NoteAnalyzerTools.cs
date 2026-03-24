using Application.Abstractions.Data;
using Domain.Notes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.AI;

internal sealed class NoteAnalyzerTools(IApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider)
{
    public async Task<object> GetPolishedContentAsync(Guid noteId)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .Select(n => new { n.ContentPolished })
            .FirstOrDefaultAsync();

        if (note is null)
            return new { error = $"Note {noteId} not found." };

        return note;
    }

    public async Task<object> GetNoteHistoryAsync(Guid noteId)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .Select(n => new { n.ContentRaw, n.ContentPolished })
            .FirstOrDefaultAsync();

        if (note is null)
            return new { error = $"Note {noteId} not found." };

        return note;
    }

    public async Task<object> SaveAnalysisAsync(Guid noteId, string summary, string keyPoints)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .FirstOrDefaultAsync();

        if (note is null)
            return new { error = $"Note {noteId} not found." };

        note.ContentSummary = summary;
        note.KeyPoints = keyPoints;
        note.Status = NoteStatus.AIReady;
        note.UpdatedAtUtc = dateTimeProvider.UtcNow;

        await dbContext.SaveChangesAsync();

        return new { success = true };
    }
}
