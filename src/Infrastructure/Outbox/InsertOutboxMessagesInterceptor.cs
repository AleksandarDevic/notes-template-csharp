using System.Text.Json;
using Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel;

namespace Infrastructure.Outbox;

internal sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            InsertOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void InsertOutboxMessages(DbContext context)
    {
        var outboxMessages = context.ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                IReadOnlyCollection<IDomainEvent> domainEvents = entity.DomainEvents;

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().FullName,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredOnUtc = DateTime.UtcNow,
            })
            .ToList();

        context.Set<OutboxMessage>().AddRange(outboxMessages);
    }
}