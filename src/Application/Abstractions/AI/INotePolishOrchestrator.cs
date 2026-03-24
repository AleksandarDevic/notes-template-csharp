namespace Application.Abstractions.AI;

public interface INotePolishOrchestrator
{
    Task ExecuteAsync(Guid noteId, CancellationToken cancellationToken = default);
}
