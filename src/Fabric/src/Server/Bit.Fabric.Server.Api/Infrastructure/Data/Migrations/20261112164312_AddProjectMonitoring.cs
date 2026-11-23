using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddProjectMonitoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ServerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TemplateVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                HasStaticFrontEnd = table.Column<bool>(type: "boolean", nullable: false),
                Health = table.Column<int>(type: "integer", nullable: false),
                ConnectionStatus = table.Column<int>(type: "integer", nullable: false),
                ConnectionProblem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
                table.UniqueConstraint("AK_Projects_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Projects_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectCheckPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CheckKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectCheckPeriods", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectCheckPeriods_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectCheckPeriods_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectConnections",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Issuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                TokenEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Resource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Scopes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ClientId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ProtectedAccessToken = table.Column<string>(type: "text", nullable: false),
                ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConnectedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ConnectedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectConnections", x => x.ProjectId);
                table.ForeignKey(
                    name: "FK_ProjectConnections_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectConnections_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectProbes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Outcome = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: true),
                ResponseMs = table.Column<int>(type: "integer", nullable: true),
                ReportMs = table.Column<int>(type: "integer", nullable: true),
                HasCacheWarning = table.Column<bool>(type: "boolean", nullable: false),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                InstanceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Problem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Checks = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectProbes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectProbes_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectProbes_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectStarts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                InstanceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsDeploy = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectStarts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectStarts_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectStarts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_ProjectId_CheckKey_StartedAt",
            table: "ProjectCheckPeriods",
            columns: new[] { "ProjectId", "CheckKey", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_TenantId_ProjectId",
            table: "ProjectCheckPeriods",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectConnections_TenantId_ProjectId",
            table: "ProjectConnections",
            columns: new[] { "TenantId", "ProjectId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_ProjectId_At",
            table: "ProjectProbes",
            columns: new[] { "ProjectId", "At" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_TenantId_ProjectId",
            table: "ProjectProbes",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId_Address",
            table: "Projects",
            columns: new[] { "TenantId", "Address" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStarts_ProjectId_At",
            table: "ProjectStarts",
            columns: new[] { "ProjectId", "At" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStarts_TenantId_ProjectId",
            table: "ProjectStarts",
            columns: new[] { "TenantId", "ProjectId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectCheckPeriods");

        migrationBuilder.DropTable(
            name: "ProjectConnections");

        migrationBuilder.DropTable(
            name: "ProjectProbes");

        migrationBuilder.DropTable(
            name: "ProjectStarts");

        migrationBuilder.DropTable(
            name: "Projects");
    }
}
