using Microsoft.EntityFrameworkCore;

namespace PamGateway.Data;

public sealed class PamGatewayDbContext : DbContext
{
    public PamGatewayDbContext(DbContextOptions<PamGatewayDbContext> options)
        : base(options)
    {
    }

    public DbSet<TargetEntity> Targets => Set<TargetEntity>();
    public DbSet<AccessRequestEntity> AccessRequests => Set<AccessRequestEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<SessionRecordingEntity> SessionRecordings => Set<SessionRecordingEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PolicyEntity> Policies => Set<PolicyEntity>();
    public DbSet<ApprovalEntity> Approvals => Set<ApprovalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TargetEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<AccessRequestEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<SessionEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<SessionRecordingEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<AuditEventEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<RoleEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<PolicyEntity>().HasKey(item => item.Id);
        modelBuilder.Entity<ApprovalEntity>().HasKey(item => item.Id);

        modelBuilder.Entity<RoleEntity>().HasData(
            new RoleEntity { Id = "ROLE-PAM-ADMIN", Name = "PAM_Administrator", Description = "Full PAM administration" },
            new RoleEntity { Id = "ROLE-AUDITOR", Name = "Security_Auditor", Description = "Read-only audit access" },
            new RoleEntity { Id = "ROLE-WIN-ADMIN", Name = "System_Admin_Windows", Description = "Windows administration" },
            new RoleEntity { Id = "ROLE-LIN-ADMIN", Name = "System_Admin_Linux", Description = "Linux administration" }
        );

        modelBuilder.Entity<PolicyEntity>().HasData(
            new PolicyEntity
            {
                Id = "POL-RDP-REMOTE",
                Name = "RDP Remote Desktop",
                TargetType = "Remote Desktop",
                AllowedProtocols = "RDP",
                Effect = "Allow",
                TargetLabelSelectorJson = "{\"os\":\"windows\",\"access\":\"rdp\"}"
            },
            new PolicyEntity
            {
                Id = "POL-SSH-SERVER",
                Name = "SSH Servers",
                TargetType = "Server",
                AllowedProtocols = "SSH",
                Effect = "Allow",
                TargetLabelSelectorJson = "{\"os\":\"linux\"}"
            }
        );

        modelBuilder.Entity<TargetEntity>().HasData(
            new TargetEntity
            {
                Id = "SCH-249443",
                Name = "Терминальная ферма",
                Host = "rdp.local",
                Port = 3389,
                LabelsJson = "{\"os\":\"windows\",\"access\":\"rdp\"}",
                Type = "Remote Desktop",
                Environment = "prod",
                Criticality = "critical",
                Status = "Используется"
            },
            new TargetEntity
            {
                Id = "SCH-229958",
                Name = "Active Directory",
                Host = "ad.local",
                Port = 389,
                LabelsJson = "{\"os\":\"windows\",\"role\":\"ad\"}",
                Type = "Active Directory",
                Environment = "prod",
                Criticality = "non-critical",
                Status = "Используется"
            }
        );
    }
}
