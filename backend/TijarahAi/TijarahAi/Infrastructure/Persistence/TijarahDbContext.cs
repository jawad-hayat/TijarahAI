using Microsoft.EntityFrameworkCore;
using TijarahAi.Domain.Entities;

namespace TijarahAi.Infrastructure.Persistence;

public class TijarahDbContext : DbContext
{
    public TijarahDbContext(DbContextOptions<TijarahDbContext> options) : base(options) { }

    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<DocumentHash> DocumentHashes => Set<DocumentHash>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentHash>(entity =>
        {
            entity.HasKey(e => e.FileName);
            entity.Property(e => e.Sha256Hash).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardOrBook).HasMaxLength(256);
            entity.Property(e => e.Topic).HasMaxLength(256);
            entity.Property(e => e.SectionTitle).HasMaxLength(256);
            entity.Property(e => e.DocumentHash).HasMaxLength(64);
        });
    }
}
