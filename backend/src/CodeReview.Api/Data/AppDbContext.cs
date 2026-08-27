using CodeReview.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeReview.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ReviewReportEntity> ReviewReports => Set<ReviewReportEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewReportEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequirementCoverageJson).HasColumnType("jsonb");
            entity.Property(e => e.FindingsJson).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.Owner, e.Repository, e.PullRequestNumber });
        });
    }
}
