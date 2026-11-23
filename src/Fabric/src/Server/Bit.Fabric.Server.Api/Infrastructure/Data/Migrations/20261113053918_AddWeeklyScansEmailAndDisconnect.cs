using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddWeeklyScansEmailAndDisconnect : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // On for every project that has its settings saved already, as for one that doesn't: the email is on until turned off.
        migrationBuilder.AddColumn<bool>(
            name: "WeeklyEmail",
            table: "ProjectScanSettings",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            name: "ProjectScansEmails",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                WeekOf = table.Column<DateOnly>(type: "date", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScansEmails", x => new { x.TenantId, x.UserId, x.WeekOf });
                table.ForeignKey(
                    name: "FK_ProjectScansEmails_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScansEmails_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScansEmails_UserId",
            table: "ProjectScansEmails",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScansEmails_WeekOf",
            table: "ProjectScansEmails",
            column: "WeekOf");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectScansEmails");

        migrationBuilder.DropColumn(
            name: "WeeklyEmail",
            table: "ProjectScanSettings");
    }
}
