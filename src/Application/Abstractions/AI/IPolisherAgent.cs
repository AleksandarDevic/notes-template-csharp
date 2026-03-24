namespace Application.Abstractions.AI;

public interface IPolisherAgent
{
    Task RunAsync(Guid noteId, CancellationToken cancellationToken = default);
}
