using Microsoft.Extensions.Logging;

namespace Infrastructure.Outbox;

internal static partial class OutboxProcessorLoggers
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox processing completed. Total time: {TotalTime}ms, Query time: {QueryTime}ms, Publish time: {PublishTime}ms, Update time: {UpdateTime}ms, Messages processed: {MessageCount}")]
    internal static partial void LogProcessingPerformance(ILogger logger, long totalTime, long queryTime, long publishTime, long updateTime, int messageCount);
}
