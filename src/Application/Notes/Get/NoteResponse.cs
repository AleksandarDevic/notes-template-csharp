using Domain.Notes;

namespace Application.Notes.Get;

public sealed record NoteResponse
{
    public Guid Id { get; init; }

    public required string Title { get; init; }
    public required string ContentRaw { get; init; }
    public string? ContentPolished { get; init; }

    public required NoteStatus Status { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }
}
