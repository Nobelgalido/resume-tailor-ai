using Microsoft.EntityFrameworkCore;

namespace ResumeTailorAI.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TailorLog> TailorLogs => Set<TailorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TailorLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.ClientHash).HasMaxLength(64);
            entity.HasIndex(e => e.CreatedUtc);
        });
    }
}
