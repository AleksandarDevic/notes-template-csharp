using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 10000)]
    public int BatchSize { get; init; } = 1000;

    [Range(1, 10)]
    public int MaxRetryCount { get; init; } = 3;

    [Range(1, 3600)]
    public int ProcessorFrequencySeconds { get; init; } = 10;

    [Range(1, 10)]
    public int MaxParallelism { get; init; } = 1;
}
