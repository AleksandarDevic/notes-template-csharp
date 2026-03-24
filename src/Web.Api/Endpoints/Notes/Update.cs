using Application.Abstractions.Messaging;
using Application.Notes.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("notes/{id:guid}", async (
            Guid id,
            Request request,
            ICommandHandler<UpdateNoteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateNoteCommand
            {
                NoteId = id,
                Title = request.Title,
                Content = request.Content
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}
