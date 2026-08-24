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
    modelBuilder.Entity<Place>()
        .HasIndex(x => new { x.Provider, x.ExternalId })
        .IsUnique();

    base.OnModelCreating(modelBuilder);
}
}