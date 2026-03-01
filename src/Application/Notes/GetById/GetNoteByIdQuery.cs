using Application.Abstractions.Messaging;

namespace Application.Notes.GetById;

public sealed record GetNoteByIdQuery(Guid NoteId) : IQuery<NoteResponse>;
