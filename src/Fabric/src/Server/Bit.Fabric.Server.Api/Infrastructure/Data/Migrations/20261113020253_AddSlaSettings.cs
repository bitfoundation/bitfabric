using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSlaSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectSlaSettings",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetPercent = table.Column<double>(type: "double precision", nullable: false),
                PartialWeight = table.Column<double>(type: "double precision", nullable: false),
                DegradedWeight = table.Column<double>(type: "double precision", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Credits = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectSlaSettings", x => x.ProjectId);
                table.ForeignKey(
                    name: "FK_ProjectSlaSettings_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectSlaSettings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectSlaSettings_TenantId_ProjectId",
            table: "ProjectSlaSettings",
            columns: new[] { "TenantId", "ProjectId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectSlaSettings");
    }
}
