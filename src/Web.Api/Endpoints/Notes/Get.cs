using Application.Abstractions.Messaging;
using Application.Notes.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("notes", async (
            IQueryHandler<GetNotesQuery,
            List<NoteResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetNotesQuery();

            Result<List<NoteResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
