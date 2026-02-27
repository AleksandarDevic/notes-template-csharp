using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Domain.Outbox;
using Npgsql;

namespace Worker.Service.Outbox;

internal sealed class OutboxProcessor(NpgsqlDataSource dataSource)
{
    private const int BatchSize = 1000;
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();

    public async Task<int> Execute(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

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

        var updateQueue = new ConcurrentQueue<OutboxUpdate>();

        foreach (var outboxMessage in outboxMessages)
        {
            try
            {
                var messageType = GetOrAddMessageType(outboxMessage.Type);
                var deserializedMessage = JsonSerializer.Deserialize(outboxMessage.Content, messageType);

                // TODO: Process the deserialized message (e.g., publish to a message broker, invoke handlers, etc.)

                updateQueue.Enqueue(new OutboxUpdate { Id = outboxMessage.Id, ProcessedOnUtc = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                updateQueue.Enqueue(new OutboxUpdate { Id = outboxMessage.Id, ProcessedOnUtc = DateTime.UtcNow, Error = ex.ToString() });
            }
        }

        foreach (var message in updateQueue)
        {
            await connection.ExecuteAsync(
                """
                    UPDATE outbox_messages
                    SET processed_on_utc = @ProcessedOnUtc, error = @Error
                    WHERE id = @Id
                    """,
                new { ProcessedOnUtc = message.ProcessedOnUtc, Error = message.Error, Id = message.Id },
                transaction: transaction);
        }

        await transaction.CommitAsync(cancellationToken);

        return outboxMessages.Count;
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