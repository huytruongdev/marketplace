using Listings.CommandService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Listings.CommandService.Infrastructure;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>(b =>
        {
            b.ToTable("listings");
            b.HasKey(x => x.Id);

            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.CategoryId).HasMaxLength(50).IsRequired();
            b.Property(x => x.BrandId).HasMaxLength(50).IsRequired();
            b.Property(x => x.City).HasMaxLength(50).IsRequired();
            b.Property(x => x.Condition).HasMaxLength(20).IsRequired();

            b.HasIndex(x => x.CreatedAtUtc);
            b.HasIndex(x => new { x.CategoryId, x.BrandId, x.City });
            b.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);

            b.Property(x => x.Type).HasMaxLength(200).IsRequired();
            b.Property(x => x.PayloadJson).IsRequired();

            b.HasIndex(x => x.PublishedAtUtc);
            b.HasIndex(x => x.OccurredAtUtc);
        });
    }
}
