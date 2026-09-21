using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<EstateServer> EstateServers => Set<EstateServer>();
    public DbSet<CtApplication> CtApplications => Set<CtApplication>();
    public DbSet<CtDatabase> CtDatabases => Set<CtDatabase>();
    public DbSet<CtServer> CtServers => Set<CtServer>();
    public DbSet<CtCost> CtCosts => Set<CtCost>();
    public DbSet<CtApplicationServer> CtApplicationServers => Set<CtApplicationServer>();
    public DbSet<AssessmentRun> AssessmentRuns => Set<AssessmentRun>();
    public DbSet<AssessmentFinding> AssessmentFindings => Set<AssessmentFinding>();
    public DbSet<AssessmentServerSnapshot> AssessmentServerSnapshots => Set<AssessmentServerSnapshot>();
    public DbSet<AssessmentDatabase> AssessmentDatabases => Set<AssessmentDatabase>();
    public DbSet<AssessmentVolume> AssessmentVolumes => Set<AssessmentVolume>();
    public DbSet<AssessmentService> AssessmentServices => Set<AssessmentService>();
    public DbSet<AssessmentWait> AssessmentWaits => Set<AssessmentWait>();
    public DbSet<AssessmentJob> AssessmentJobs => Set<AssessmentJob>();
    public DbSet<AssessmentSysadmin> AssessmentSysadmins => Set<AssessmentSysadmin>();
    public DbSet<AssessmentConfiguration> AssessmentConfigurations => Set<AssessmentConfiguration>();
    public DbSet<AssessmentBackup> AssessmentBackups => Set<AssessmentBackup>();
    public DbSet<AssessmentLinkedServer> AssessmentLinkedServers => Set<AssessmentLinkedServer>();
    public DbSet<AssessmentSqlLogin> AssessmentSqlLogins => Set<AssessmentSqlLogin>();
    public DbSet<AssessmentAvailabilityGroup> AssessmentAvailabilityGroups => Set<AssessmentAvailabilityGroup>();
    public DbSet<AssessmentCertificate> AssessmentCertificates => Set<AssessmentCertificate>();
    public DbSet<AssessmentTlsCertificate> AssessmentTlsCertificates => Set<AssessmentTlsCertificate>();
    public DbSet<InventorySyncBatch> InventorySyncBatches => Set<InventorySyncBatch>();
    public DbSet<InventorySyncItem> InventorySyncItems => Set<InventorySyncItem>();
    public DbSet<InventorySyncField> InventorySyncFields => Set<InventorySyncField>();
    public DbSet<InventorySyncAudit> InventorySyncAudits => Set<InventorySyncAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeamMember>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<EstateServer>()
            .HasIndex(x => x.ServerName)
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(x => new { x.AccessRoleId, x.Module })
            .IsUnique();

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.AccessRole)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.AccessRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.Team)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.AccessRole)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.AccessRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentFinding>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Findings)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentServerSnapshot>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Servers)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentDatabase>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Databases)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentVolume>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Volumes)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentService>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Services)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentWait>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Waits)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentJob>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Jobs)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentSysadmin>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Sysadmins)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentConfiguration>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Configurations)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentBackup>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Backups)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentLinkedServer>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.LinkedServers)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentSqlLogin>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.SqlLogins)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentAvailabilityGroup>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.AvailabilityGroups)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentCertificate>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Certificates)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentTlsCertificate>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.TlsCertificates)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventorySyncItem>()
            .HasOne(x => x.Batch)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventorySyncField>()
            .HasOne(x => x.Item)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventorySyncAudit>()
            .HasOne(x => x.Batch)
            .WithMany(x => x.Audits)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.MemoryMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.AllocatedGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.BatchRequestsPerSec).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentRun>().Property(x => x.AllocatedStorageGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentDatabase>().Property(x => x.DataMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentDatabase>().Property(x => x.LogMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.TotalGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.FreeGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.FreePct).HasPrecision(8, 2);
        modelBuilder.Entity<AssessmentWait>().Property(x => x.WaitPct).HasPrecision(8, 2);
    }
}
