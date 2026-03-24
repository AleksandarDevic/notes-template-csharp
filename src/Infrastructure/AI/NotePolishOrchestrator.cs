using Application.Abstractions.AI;
using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.AI;

internal sealed class NotePolishOrchestrator(
    IPolisherAgent polisherAgent,
    IAnalyzerAgent analyzerAgent,
    IApplicationDbContext dbContext)
    : INotePolishOrchestrator
{
    public async Task ExecuteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var note = await dbContext.Notes
            .Where(n => n.Id == noteId)
            .Select(n => new { n.ContentPolished, n.ContentSummary })
            .FirstOrDefaultAsync(cancellationToken);

        if (note is null)
            return;

        // Agent 1 — skip if already completed (retry safety)
        if (string.IsNullOrEmpty(note.ContentPolished))
            await polisherAgent.RunAsync(noteId, cancellationToken);

        // Agent 2 — skip if already completed (retry safety)
        if (string.IsNullOrEmpty(note.ContentSummary))
            await analyzerAgent.RunAsync(noteId, cancellationToken);
    }
}
