using BitrixChecker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BitrixChecker.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
    {
        public DbSet<LinkResult> LinkResults { get; set; }
        public DbSet<ScanJob> ScanJobs { get; set; }
        public DbSet<LinkProcessing> LinkProcessings { get; set; }
        public DbSet<ProcessingHistory> ProcessingHistories { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(x => new { x.EntityName, x.EntityId });
            });

            builder.Entity<LinkResult>(entity =>
            {
                entity.HasIndex(x => x.Subdomain).HasDatabaseName("IX_CheckedLinks_Subdomain");
                entity.HasIndex(x => new { x.Status, x.LastChecked });

                entity.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(x => x.ScanJob)
                    .WithMany(x => x.LinkResults)
                    .HasForeignKey(x => x.ScanJobId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ScanJob>(entity =>
            {
                entity.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<LinkProcessing>(entity =>
            {
                entity.HasIndex(x => x.LinkResultId);
                entity.HasIndex(x => new { x.AssignedUserId, x.Status, x.UpdatedAt });
                entity.HasOne(x => x.LinkResult)
                    .WithMany(x => x.Processings)
                    .HasForeignKey(x => x.LinkResultId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.AssignedUser)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(x => x.UpdatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ProcessingHistory>(entity =>
            {
                entity.HasIndex(x => x.LinkProcessingId);
                entity.HasIndex(x => x.ChangedAt);
                entity.HasOne(x => x.LinkProcessing)
                    .WithMany()
                    .HasForeignKey(x => x.LinkProcessingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.AssignedUser)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(x => x.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ChangedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
