using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddStatusPages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid[]>(
            name: "ComponentIds",
            table: "ProjectIncidents",
            type: "uuid[]",
            nullable: false,
            defaultValue: new Guid[0]);

        migrationBuilder.AddColumn<int>(
            name: "Kind",
            table: "ProjectIncidents",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "StatusPages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                ShowsResponseTime = table.Column<bool>(type: "boolean", nullable: false),
                Languages = table.Column<string[]>(type: "text[]", nullable: false),
                SupportAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CustomHostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                CustomHostnameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CustomHostnameState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusPages", x => x.Id);
                table.UniqueConstraint("AK_StatusPages_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_StatusPages_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusPages_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "StatusComponents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                StatusPageId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                CheckKeys = table.Column<string[]>(type: "text[]", nullable: false),
                IsShown = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Description = table.Column<string>(type: "jsonb", nullable: true),
                Name = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusComponents", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusComponents_StatusPages_TenantId_StatusPageId",
                    columns: x => new { x.TenantId, x.StatusPageId },
                    principalTable: "StatusPages",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusComponents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "StatusPageFollowers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                StatusPageId = table.Column<Guid>(type: "uuid", nullable: false),
                PushNotificationSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ComponentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                FollowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusPageFollowers", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusPageFollowers_PushNotificationSubscriptions_PushNotif~",
                    column: x => x.PushNotificationSubscriptionId,
                    principalTable: "PushNotificationSubscriptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusPageFollowers_StatusPages_StatusPageId",
                    column: x => x.StatusPageId,
                    principalTable: "StatusPages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StatusComponents_TenantId_StatusPageId",
            table: "StatusComponents",
            columns: new[] { "TenantId", "StatusPageId" });

        migrationBuilder.CreateIndex(
            name: "IX_StatusPageFollowers_PushNotificationSubscriptionId",
            table: "StatusPageFollowers",
            column: "PushNotificationSubscriptionId");

        migrationBuilder.CreateIndex(
            name: "IX_StatusPageFollowers_StatusPageId_PushNotificationSubscripti~",
            table: "StatusPageFollowers",
            columns: new[] { "StatusPageId", "PushNotificationSubscriptionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_CustomHostname",
            table: "StatusPages",
            column: "CustomHostname",
            unique: true,
            filter: "\"CustomHostname\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_ProjectId",
            table: "StatusPages",
            column: "ProjectId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_Slug",
            table: "StatusPages",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_TenantId_ProjectId",
            table: "StatusPages",
            columns: new[] { "TenantId", "ProjectId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StatusComponents");

        migrationBuilder.DropTable(
            name: "StatusPageFollowers");

        migrationBuilder.DropTable(
            name: "StatusPages");

        migrationBuilder.DropColumn(
            name: "ComponentIds",
            table: "ProjectIncidents");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "ProjectIncidents");
    }
}
