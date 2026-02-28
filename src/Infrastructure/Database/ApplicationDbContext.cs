using Application.Abstractions.Data;
using Domain.Notes;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Note> Notes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.ApplyUtcDateTimeConverter();

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // When should you publish domain events?
        //
        // 1. BEFORE calling SaveChangesAsync
        //     - domain events are part of the same transaction
        //     - immediate consistency
        // 2. AFTER calling SaveChangesAsync
        //     - domain events are a separate transaction
        //     - eventual consistency
        //     - handlers can fail

        // await PublishDomainEventsAsync();

        int result = await base.SaveChangesAsync(cancellationToken);

        return result;
    }

    // private async Task PublishDomainEventsAsync()
    // {
    //     var domainEvents = ChangeTracker
    //         .Entries<Entity>()
    //         .Select(entry => entry.Entity)
    //         .SelectMany(entity =>
    //         {
    //             List<IDomainEvent> domainEvents = entity.DomainEvents;

    //             entity.ClearDomainEvents();

    //             return domainEvents;
    //         })
    //         .ToList();

    //     await domainEventsDispatcher.DispatchAsync(domainEvents);
    // }
}
