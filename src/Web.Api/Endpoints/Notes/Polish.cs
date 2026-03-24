using Application.Abstractions.Messaging;
using Application.Notes.Polish;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Polish : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("notes/{id:guid}/polish", async (
            Guid id,
            ICommandHandler<PolishNoteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new PolishNoteCommand { NoteId = id };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
