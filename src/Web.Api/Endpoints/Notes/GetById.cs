using Application.Abstractions.Messaging;
using Application.Notes.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("notes/{id:guid}", async (
            Guid id,
            IQueryHandler<GetNoteByIdQuery, NoteResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetNoteByIdQuery(id);

            Result<NoteResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
