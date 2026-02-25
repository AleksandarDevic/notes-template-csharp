using Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasMaxLength(5000)
            .HasColumnType("jsonb");

        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();

        // Partial covering index (PostgreSQL)
        builder.HasIndex(x => new { x.OccurredOnUtc, x.ProcessedOnUtc })
            .HasDatabaseName("idx_outbox_messages_unprocessed")
            .IncludeProperties(x => new { x.Id, x.Type, x.Content })
            .HasFilter("processed_on_utc IS NULL");
    }
}
