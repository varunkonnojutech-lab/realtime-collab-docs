using CollabDocs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CollabDocs.Api.Data;

public class CollabDocsDbContext : DbContext
{
    public CollabDocsDbContext(DbContextOptions<CollabDocsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
