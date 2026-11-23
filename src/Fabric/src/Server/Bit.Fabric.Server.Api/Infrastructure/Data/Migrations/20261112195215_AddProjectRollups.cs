using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddProjectRollups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectRollups",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                BucketStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CheckKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                FailedCount = table.Column<int>(type: "integer", nullable: false),
                DegradedCount = table.Column<int>(type: "integer", nullable: false),
                TimedCount = table.Column<int>(type: "integer", nullable: false),
                TotalMs = table.Column<long>(type: "bigint", nullable: false),
                MaxMs = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectRollups", x => new { x.ProjectId, x.BucketStart, x.Region, x.CheckKey });
                table.ForeignKey(
                    name: "FK_ProjectRollups_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectRollups_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_At",
            table: "ProjectProbes",
            column: "At");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_ProjectId",
            table: "ProjectCheckPeriods",
            column: "ProjectId",
            filter: "\"EndedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_BucketStart",
            table: "ProjectRollups",
            column: "BucketStart");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_ProjectId_CheckKey_BucketStart",
            table: "ProjectRollups",
            columns: new[] { "ProjectId", "CheckKey", "BucketStart" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_TenantId_ProjectId",
            table: "ProjectRollups",
            columns: new[] { "TenantId", "ProjectId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectRollups");

        migrationBuilder.DropIndex(
            name: "IX_ProjectProbes_At",
            table: "ProjectProbes");

        migrationBuilder.DropIndex(
            name: "IX_ProjectCheckPeriods_ProjectId",
            table: "ProjectCheckPeriods");
    }
}
