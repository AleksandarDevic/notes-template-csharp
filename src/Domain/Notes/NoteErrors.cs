using SharedKernel;

namespace Domain.Notes;

public static class NoteErrors
{
    public static Error NotFound(Guid noteId) => Error.NotFound(
        "Notes.NotFound",
        $"The note with Id = '{noteId}' was not found.");
}