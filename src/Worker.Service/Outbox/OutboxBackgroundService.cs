using Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace Worker.Service.Outbox;

internal sealed class OutboxBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;
    private int _totalIteration = 0;
    private int _totalProcessedMessages = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        OutboxLoggers.LogStarting(logger);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxParallelism,
            CancellationToken = stoppingToken
        };

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, _options.MaxParallelism),
                parallelOptions, async (index, cancellationToken) =>
                {
                    await ProcessOutboxMessages(cancellationToken);
                });
        }
        catch (OperationCanceledException)
        {
            OutboxLoggers.LogOperationCancelled(logger);
        }
        catch (Exception ex)
        {
            OutboxLoggers.LogError(logger, ex);
        }
        finally
        {
            OutboxLoggers.LogFinished(logger, _totalIteration, _totalProcessedMessages);
        }
    }

    private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                var iterationCount = Interlocked.Increment(ref _totalIteration);

                OutboxLoggers.LogStartingIteration(logger, iterationCount);

                int processedMessages = await outboxProcessor.Execute(cancellationToken);
                var totalProcessedMessages = Interlocked.Add(ref _totalProcessedMessages, processedMessages);

                OutboxLoggers.LogIterationCompleted(logger, iterationCount, processedMessages, totalProcessedMessages);

                if (processedMessages == 0)
                    await Task.Delay(TimeSpan.FromSeconds(_options.ProcessorFrequencySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                OutboxLoggers.LogError(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(_options.ProcessorFrequencySeconds), cancellationToken);
            }
        }
    }
}