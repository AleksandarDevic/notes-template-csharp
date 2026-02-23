using Application.Abstractions.Messaging;

namespace Application.Notes.Get;

public sealed record GetNotesQuery : IQuery<List<NoteResponse>>;
