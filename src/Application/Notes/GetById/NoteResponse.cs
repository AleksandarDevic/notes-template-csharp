using Domain.Notes;

namespace Application.Notes.GetById;

public sealed record NoteResponse
{
    public Guid Id { get; init; }

    public required string Title { get; init; }
    public required string ContentRaw { get; init; }
    public string? ContentPolished { get; init; }

    public required NoteStatus Status { get; init; }

    public DateTime LastUpdatedAtUtc { get; init; }
}
