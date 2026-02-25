namespace Worker.Service.Outbox;

internal sealed class OutboxBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    private const int OutboxProcessorFrequency = 10;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("OutboxBackgroundService starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = serviceScopeFactory.CreateScope();
                var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await outboxProcessor.Execute(stoppingToken);
            }

            // Simulate running Outbox processing every N seconds
            await Task.Delay(TimeSpan.FromSeconds(OutboxProcessorFrequency), stoppingToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "OutboxBackgroundService stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in OutboxBackgroundService");
        }
        finally
        {
            logger.LogInformation("OutboxBackgroundService finished.");
        }
    }
}