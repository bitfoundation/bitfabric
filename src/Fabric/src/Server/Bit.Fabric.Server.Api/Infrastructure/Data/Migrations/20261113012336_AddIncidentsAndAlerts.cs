using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddIncidentsAndAlerts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TimeZoneId",
            table: "Projects",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "UTC");

        migrationBuilder.CreateTable(
            name: "ProjectAlertRoutes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Step = table.Column<int>(type: "integer", nullable: false),
                DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                Channel = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                WebhookFormat = table.Column<int>(type: "integer", nullable: true),
                Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAlertRoutes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectIncidents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Impact = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CheckKeys = table.Column<string[]>(type: "text[]", nullable: false),
                IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                AfterDeployAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DuringMaintenance = table.Column<bool>(type: "boolean", nullable: false),
                AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                AcknowledgedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EscalationStep = table.Column<int>(type: "integer", nullable: false),
                NextEscalationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Title = table.Column<string>(type: "jsonb", nullable: true),
                Updates = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectIncidents", x => x.Id);
                table.UniqueConstraint("AK_ProjectIncidents_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProjectIncidents_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectIncidents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectMaintenanceWindows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RepeatsWeekly = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectMaintenanceWindows", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectMaintenanceWindows_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectMaintenanceWindows_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectAlerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Channel = table.Column<int>(type: "integer", nullable: false),
                Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                Problem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                ProblemStatusCode = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAlerts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_ProjectIncidents_TenantId_IncidentId",
                    columns: x => new { x.TenantId, x.IncidentId },
                    principalTable: "ProjectIncidents",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlertRoutes_ProjectId_Step",
            table: "ProjectAlertRoutes",
            columns: new[] { "ProjectId", "Step" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlertRoutes_TenantId_ProjectId",
            table: "ProjectAlertRoutes",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlertRoutes_UserId",
            table: "ProjectAlertRoutes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_IncidentId",
            table: "ProjectAlerts",
            column: "IncidentId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_ProjectId_SentAt",
            table: "ProjectAlerts",
            columns: new[] { "ProjectId", "SentAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_TenantId_IncidentId",
            table: "ProjectAlerts",
            columns: new[] { "TenantId", "IncidentId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_TenantId_ProjectId",
            table: "ProjectAlerts",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_ProjectId",
            table: "ProjectIncidents",
            column: "ProjectId",
            filter: "\"EndedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_ProjectId_StartedAt",
            table: "ProjectIncidents",
            columns: new[] { "ProjectId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_TenantId_ProjectId",
            table: "ProjectIncidents",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMaintenanceWindows_ProjectId_StartsAt",
            table: "ProjectMaintenanceWindows",
            columns: new[] { "ProjectId", "StartsAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMaintenanceWindows_TenantId_ProjectId",
            table: "ProjectMaintenanceWindows",
            columns: new[] { "TenantId", "ProjectId" });

        // Projects added before alerts existed get what a new one gets: a push to whoever connected it at once, and an
        // email 10 minutes later if nobody is on it yet (See ProjectsController.CompleteConnect).
        migrationBuilder.Sql("""
            INSERT INTO "ProjectAlertRoutes" ("ProjectId", "TenantId", "Step", "DelayMinutes", "Channel", "UserId", "Culture")
            SELECT p."Id", p."TenantId", 0, 0, 0, c."ConnectedByUserId", 'en-US'
            FROM "Projects" p JOIN "ProjectConnections" c ON c."ProjectId" = p."Id"
            UNION ALL
            SELECT p."Id", p."TenantId", 1, 10, 1, c."ConnectedByUserId", 'en-US'
            FROM "Projects" p JOIN "ProjectConnections" c ON c."ProjectId" = p."Id";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectAlertRoutes");

        migrationBuilder.DropTable(
            name: "ProjectAlerts");

        migrationBuilder.DropTable(
            name: "ProjectMaintenanceWindows");

        migrationBuilder.DropTable(
            name: "ProjectIncidents");

        migrationBuilder.DropColumn(
            name: "TimeZoneId",
            table: "Projects");
    }
}
