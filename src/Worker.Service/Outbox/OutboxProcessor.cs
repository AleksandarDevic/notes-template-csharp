using System.Text.Json;
using Dapper;
using Domain.Outbox;
using Npgsql;

namespace Worker.Service.Outbox;

internal sealed class OutboxProcessor(NpgsqlDataSource dataSource)
{
    private const int BatchSize = 10;

    public async Task<int> Execute(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var outboxMessages = (await connection.QueryAsync<OutboxMessage>(
            """
            SELECT *
            FROM outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc LIMIT @BatchSize
            """,
            new { BatchSize },
            transaction: transaction)).AsList();

        foreach (var outboxMessage in outboxMessages)
        {
            try
            {
                var messageType = Domain.AssemblyReference.Assembly.GetType(outboxMessage.Type)!;
                var deserializedMessage = JsonSerializer.Deserialize(outboxMessage.Content, messageType);

                // TODO: Process the deserialized message (e.g., publish to a message broker, invoke handlers, etc.)
                

                await connection.ExecuteAsync(
                    """
                    UPDATE outbox_messages
                    SET processed_on_utc = @ProcessedOnUtc
                    WHERE id = @Id
                    """,
                    new { ProcessedOnUtc = DateTime.UtcNow, outboxMessage.Id },
                    transaction: transaction);
            }
            catch (Exception ex)
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE outbox_messages
                    SET processed_on_utc = @ProcessedOnUtc, error = @Error
                    WHERE id = @Id
                    """,
                    new { ProcessedOnUtc = DateTime.UtcNow, Error = ex.ToString(), outboxMessage.Id },
                    transaction: transaction);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return outboxMessages.Count;
    }
}