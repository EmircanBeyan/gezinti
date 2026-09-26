using Gezinti.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gezinti.Infrastructure.Context;

public class GezintiDbContext : DbContext
{
    public GezintiDbContext(DbContextOptions<GezintiDbContext> options)
        : base(options)
    {
    }

    public DbSet<Place> Places => Set<Place>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Place>(entity =>
        {
            entity.HasIndex(x => new
            {
                x.Provider,
                x.ExternalId
            }).IsUnique();

            entity.Property(x => x.Location)
                .HasColumnType("geography (point, 4326)");
        });
    }
}