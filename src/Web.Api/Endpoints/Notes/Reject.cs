using Application.Abstractions.Messaging;
using Application.Notes.Reject;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Reject : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("notes/{id:guid}/reject", async (
            Guid id,
            ICommandHandler<RejectNoteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RejectNoteCommand { NoteId = id };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
