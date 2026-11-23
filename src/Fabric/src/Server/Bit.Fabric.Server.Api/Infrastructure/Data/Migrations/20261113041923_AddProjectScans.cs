using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddProjectScans : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectScanFindingReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                FindingId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                FindingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Where = table.Column<string[]>(type: "text[]", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanFindingReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Trigger = table.Column<int>(type: "integer", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Outcome = table.Column<int>(type: "integer", nullable: true),
                Verdict = table.Column<int>(type: "integer", nullable: true),
                Score = table.Column<int>(type: "integer", nullable: true),
                Grade = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScans", x => x.Id);
                table.UniqueConstraint("AK_ProjectScans_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProjectScans_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScans_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScans_Users_RequestedByUserId",
                    column: x => x.RequestedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScanSettings",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ScanAfterDeployments = table.Column<bool>(type: "boolean", nullable: false),
                WeeklyScans = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanSettings", x => x.ProjectId);
                table.ForeignKey(
                    name: "FK_ProjectScanSettings_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanSettings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectScanFindings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ScanId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Severity = table.Column<int>(type: "integer", nullable: false),
                Parameters = table.Column<string[]>(type: "text[]", nullable: false),
                Where = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanFindings", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScanFindings_ProjectScans_TenantId_ScanId",
                    columns: x => new { x.TenantId, x.ScanId },
                    principalTable: "ProjectScans",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanFindings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_FindingId_UserId",
            table: "ProjectScanFindingReports",
            columns: new[] { "FindingId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_FindingKey_ReportedAt",
            table: "ProjectScanFindingReports",
            columns: new[] { "FindingKey", "ReportedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_TenantId_ProjectId",
            table: "ProjectScanFindingReports",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_UserId",
            table: "ProjectScanFindingReports",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindings_TenantId_ScanId",
            table: "ProjectScanFindings",
            columns: new[] { "TenantId", "ScanId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_ProjectId_Kind_RequestedAt",
            table: "ProjectScans",
            columns: new[] { "ProjectId", "Kind", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_ProjectId_RequestedAt",
            table: "ProjectScans",
            columns: new[] { "ProjectId", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_RequestedAt",
            table: "ProjectScans",
            column: "RequestedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_RequestedByUserId",
            table: "ProjectScans",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_TenantId_ProjectId",
            table: "ProjectScans",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_Unfinished",
            table: "ProjectScans",
            column: "ProjectId",
            filter: "\"FinishedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanSettings_TenantId_ProjectId",
            table: "ProjectScanSettings",
            columns: new[] { "TenantId", "ProjectId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectScanFindingReports");

        migrationBuilder.DropTable(
            name: "ProjectScanFindings");

        migrationBuilder.DropTable(
            name: "ProjectScanSettings");

        migrationBuilder.DropTable(
            name: "ProjectScans");
    }
}
