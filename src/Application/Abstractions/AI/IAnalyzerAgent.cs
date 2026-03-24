namespace Application.Abstractions.AI;

public interface IAnalyzerAgent
{
    Task RunAsync(Guid noteId, CancellationToken cancellationToken = default);
}
