using Application.Abstractions.Messaging;
using Application.Notes.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Notes;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("notes", async (
            Request request,
            ICommandHandler<CreateNoteCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateNoteCommand
            {
                Title = request.Title,
                Content = request.Content
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Notes);
    }
}