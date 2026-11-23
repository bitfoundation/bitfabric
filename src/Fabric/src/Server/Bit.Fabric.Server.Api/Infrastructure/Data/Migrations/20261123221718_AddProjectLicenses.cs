using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class _20261123221718_AddProjectLicenses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LicensedProjects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FirstCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TrialIssuedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                TrialExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                TrialLicense = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LicensedProjects", x => x.Id);
                table.ForeignKey(
                    name: "FK_LicensedProjects_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "IssuedLicenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FirstCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Document = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ClaimCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ClaimExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClaimedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IssuedLicenses", x => x.Id);
                table.ForeignKey(
                    name: "FK_IssuedLicenses_LicensedProjects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "LicensedProjects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_IssuedLicenses_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_ClaimCodeHash",
            table: "IssuedLicenses",
            column: "ClaimCodeHash",
            filter: "\"ClaimedOn\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_ProjectId_ExpiresOn",
            table: "IssuedLicenses",
            columns: new[] { "ProjectId", "ExpiresOn" });

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_UserId",
            table: "IssuedLicenses",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_LicensedProjects_UserId",
            table: "LicensedProjects",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IssuedLicenses");

        migrationBuilder.DropTable(
            name: "LicensedProjects");
    }
}
