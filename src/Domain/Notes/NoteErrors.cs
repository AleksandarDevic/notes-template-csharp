using SharedKernel;

namespace Domain.Notes;

public static class NoteErrors
{
    public static Error NotFound(Guid noteId) => Error.NotFound(
        "Notes.NotFound",
        $"The note with Id = '{noteId}' was not found.");

    public static Error InvalidStatus(Guid noteId, NoteStatus status) => Error.Problem(
        "Notes.InvalidStatus",
        $"The note with Id = '{noteId}' has invalid status '{status}' for this operation.");
}