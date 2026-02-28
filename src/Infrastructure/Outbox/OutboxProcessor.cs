using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Domain.Outbox;
using Infrastructure.DomainEvents;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Outbox;

public sealed class OutboxProcessor(
    NpgsqlDataSource dataSource,
    IDomainEventsDispatcher domainEventsDispatcher,
    ILogger<OutboxProcessor> logger)
{
    private const int BatchSize = 1000;
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();

    public async Task<int> Execute(CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = new Stopwatch();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        stepStopwatch.Restart();
        var outboxMessages = (await connection.QueryAsync<OutboxMessage>(
            """
            SELECT id as Id, type as Type, content as Content
            FROM outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc LIMIT @BatchSize
            FOR UPDATE SKIP LOCKED
            """,
            new { BatchSize },
            transaction: transaction)).AsList();
        var queryTime = stepStopwatch.ElapsedMilliseconds;

        var updateQueue = new ConcurrentQueue<OutboxUpdate>();

        stepStopwatch.Restart();
        foreach (var outboxMessage in outboxMessages)
        {
            await ProcessMessage(outboxMessage, updateQueue, cancellationToken);
        }
        var processingTime = stepStopwatch.ElapsedMilliseconds;

        // foreach (var message in updateQueue)
        // {
        //     await connection.ExecuteAsync(
        //         """
        //             UPDATE outbox_messages
        //             SET processed_on_utc = @ProcessedOnUtc, error = @Error
        //             WHERE id = @Id
        //             """,
        //         new { ProcessedOnUtc = message.ProcessedOnUtc, Error = message.Error, Id = message.Id },
        //         transaction: transaction);
        // }

        stepStopwatch.Restart();
        if (!updateQueue.IsEmpty)
        {
            var updateSql =
                """
                UPDATE outbox_messages
                SET processed_on_utc = v.processed_on_utc, error = v.error
                FROM (VALUES {0}) AS v(id, processed_on_utc, error)
                WHERE outbox_messages.id = v.id::uuid
                """;

            var updates = updateQueue.ToList();
            var valuesList = string.Join(",",
                updates.Select((item, index) => $"(@Id{index}, @ProcessedOnUtc{index}, @Error{index})"));

            var parameters = new DynamicParameters();

            for (int i = 0; i < updates.Count; i++)
            {
                parameters.Add($"Id{i}", updates[i].Id.ToString());
                parameters.Add($"ProcessedOnUtc{i}", updates[i].ProcessedOnUtc);
                parameters.Add($"Error{i}", updates[i].Error);
            }

            var formattedSql = string.Format(updateSql, valuesList);

            await connection.ExecuteAsync(formattedSql, parameters, transaction: transaction);
        }
        var updateTime = stepStopwatch.ElapsedMilliseconds;

        await transaction.CommitAsync(cancellationToken);

        totalStopwatch.Stop();
        var totalTime = totalStopwatch.ElapsedMilliseconds;

        OutboxProcessorLoggers.LogProcessingPerformance(logger, totalTime, queryTime, processingTime, updateTime, outboxMessages.Count);

        return outboxMessages.Count;
    }

    private async Task ProcessMessage(OutboxMessage outboxMessage, ConcurrentQueue<OutboxUpdate> updateQueue, CancellationToken cancellationToken)
    {
        try
        {
            Type messageType = GetOrAddMessageType(outboxMessage.Type);
            var deserializedMessage = JsonSerializer.Deserialize(outboxMessage.Content, messageType);

            if (deserializedMessage is IDomainEvent domainEvent)
                await domainEventsDispatcher.DispatchAsync([domainEvent], cancellationToken);

            updateQueue.Enqueue(new OutboxUpdate { Id = outboxMessage.Id, ProcessedOnUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            updateQueue.Enqueue(new OutboxUpdate { Id = outboxMessage.Id, ProcessedOnUtc = DateTime.UtcNow, Error = ex.ToString() });
        }
    }

    private static Type GetOrAddMessageType(string typeName)
        => TypeCache.GetOrAdd(typeName, name => Domain.AssemblyReference.Assembly.GetType(name)!);

    private readonly struct OutboxUpdate
    {
        public Guid Id { get; init; }
        public DateTime ProcessedOnUtc { get; init; }
        public string? Error { get; init; }
    }
}
