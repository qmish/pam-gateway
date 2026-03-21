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
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<AgentTicketEntity> AgentTickets => Set<AgentTicketEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TargetEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AccessRequestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.TargetId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.TargetId);
            e.HasIndex(x => x.RequestId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<SessionRecordingEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SessionId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.TargetId);
            e.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<RoleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PolicyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<ApprovalEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AgentEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.LastSeenAt);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AgentTicketEntity>(e =>
        {
            e.HasKey(x => x.Ticket);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.AgentId);
            e.HasIndex(x => x.ExpiresAt);
        });

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
