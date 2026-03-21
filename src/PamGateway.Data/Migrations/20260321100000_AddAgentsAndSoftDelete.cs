using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PamGateway.Data.Migrations;

public partial class AddAgentsAndSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Agents",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                Hostname = table.Column<string>(nullable: false),
                Os = table.Column<string>(nullable: false),
                Status = table.Column<int>(nullable: false),
                LastSeenAt = table.Column<DateTimeOffset>(nullable: false),
                PublicUrl = table.Column<string>(nullable: false),
                LabelsJson = table.Column<string>(nullable: false, defaultValue: "{}"),
                CapabilitiesJson = table.Column<string>(nullable: false, defaultValue: "[]"),
                Token = table.Column<string>(nullable: false),
                IsDeleted = table.Column<bool>(nullable: false, defaultValue: false),
                DeletedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Agents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AgentTickets",
            columns: table => new
            {
                Ticket = table.Column<string>(nullable: false),
                SessionId = table.Column<string>(nullable: false),
                AgentId = table.Column<string>(nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentTickets", x => x.Ticket);
            });

        // Soft-delete columns for existing entities
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "Targets", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "Targets", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "AccessRequests", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "AccessRequests", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "Sessions", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "Sessions", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "SessionRecordings", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "SessionRecordings", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "Roles", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "Roles", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "Policies", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "Policies", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsDeleted", table: "Approvals", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedAt", table: "Approvals", nullable: true);

        // Indexes for Agents
        migrationBuilder.CreateIndex(name: "IX_Agents_Status", table: "Agents", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_Agents_LastSeenAt", table: "Agents", column: "LastSeenAt");

        // Indexes for AgentTickets
        migrationBuilder.CreateIndex(name: "IX_AgentTickets_SessionId", table: "AgentTickets", column: "SessionId");
        migrationBuilder.CreateIndex(name: "IX_AgentTickets_AgentId", table: "AgentTickets", column: "AgentId");
        migrationBuilder.CreateIndex(name: "IX_AgentTickets_ExpiresAt", table: "AgentTickets", column: "ExpiresAt");

        // Indexes for frequently queried columns
        migrationBuilder.CreateIndex(name: "IX_AccessRequests_Status", table: "AccessRequests", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_AccessRequests_CreatedAt", table: "AccessRequests", column: "CreatedAt");
        migrationBuilder.CreateIndex(name: "IX_AccessRequests_TargetId", table: "AccessRequests", column: "TargetId");
        migrationBuilder.CreateIndex(name: "IX_Sessions_Status", table: "Sessions", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_Sessions_TargetId", table: "Sessions", column: "TargetId");
        migrationBuilder.CreateIndex(name: "IX_Sessions_RequestId", table: "Sessions", column: "RequestId");
        migrationBuilder.CreateIndex(name: "IX_SessionRecordings_SessionId", table: "SessionRecordings", column: "SessionId");
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_Timestamp", table: "AuditEvents", column: "Timestamp");
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_UserId", table: "AuditEvents", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_TargetId", table: "AuditEvents", column: "TargetId");
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_EventType", table: "AuditEvents", column: "EventType");
        migrationBuilder.CreateIndex(name: "IX_Targets_Status", table: "Targets", column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Agents");
        migrationBuilder.DropTable(name: "AgentTickets");

        migrationBuilder.DropColumn(name: "IsDeleted", table: "Targets");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Targets");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "AccessRequests");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "AccessRequests");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "Sessions");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Sessions");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "SessionRecordings");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "SessionRecordings");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "Roles");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Roles");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "Policies");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Policies");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "Approvals");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Approvals");

        migrationBuilder.DropIndex(name: "IX_Agents_Status", table: "Agents");
        migrationBuilder.DropIndex(name: "IX_Agents_LastSeenAt", table: "Agents");
        migrationBuilder.DropIndex(name: "IX_AgentTickets_SessionId", table: "AgentTickets");
        migrationBuilder.DropIndex(name: "IX_AgentTickets_AgentId", table: "AgentTickets");
        migrationBuilder.DropIndex(name: "IX_AgentTickets_ExpiresAt", table: "AgentTickets");
        migrationBuilder.DropIndex(name: "IX_AccessRequests_Status", table: "AccessRequests");
        migrationBuilder.DropIndex(name: "IX_AccessRequests_CreatedAt", table: "AccessRequests");
        migrationBuilder.DropIndex(name: "IX_AccessRequests_TargetId", table: "AccessRequests");
        migrationBuilder.DropIndex(name: "IX_Sessions_Status", table: "Sessions");
        migrationBuilder.DropIndex(name: "IX_Sessions_TargetId", table: "Sessions");
        migrationBuilder.DropIndex(name: "IX_Sessions_RequestId", table: "Sessions");
        migrationBuilder.DropIndex(name: "IX_SessionRecordings_SessionId", table: "SessionRecordings");
        migrationBuilder.DropIndex(name: "IX_AuditEvents_Timestamp", table: "AuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuditEvents_UserId", table: "AuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuditEvents_TargetId", table: "AuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuditEvents_EventType", table: "AuditEvents");
        migrationBuilder.DropIndex(name: "IX_Targets_Status", table: "Targets");
    }
}
