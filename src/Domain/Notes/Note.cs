using SharedKernel;

namespace Domain.Notes;

public sealed class Note : Entity
{
    public Guid Id { get; set; }

    public required string Title { get; set; }
    public string ContentRaw { get; set; } = string.Empty;
    public string? ContentPolished { get; set; }

    public required NoteStatus Status { get; set; }


    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
