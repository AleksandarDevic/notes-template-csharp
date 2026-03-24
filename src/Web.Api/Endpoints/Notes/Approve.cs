using Application.Abstractions.Messaging;
using Application.Notes.Approve;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Approve : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("notes/{id:guid}/approve", async (
            Guid id,
            ICommandHandler<ApproveNoteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ApproveNoteCommand { NoteId = id };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
