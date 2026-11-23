using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <summary>
/// Template 11.0.0-pre-21 renamed TenantUser to TenantMembership. EF Core scaffolds that as a dropped table and a new one,
/// which would lose every tenant's members, so the table, its keys and its indexes are renamed in place instead. The
/// migrations run while the previous version still serves, and it reads and writes "TenantUsers", so that name stays as a
/// view over the table, which PostgreSQL updates through as it does the table, until a later release drops it (See
/// bitfabric-internal/specs/workarounds.md).
/// </summary>
public partial class RenameTenantUsersToTenantMemberships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "TenantUsers",
            newName: "TenantMemberships");

        RenameKeysAndIndexes(migrationBuilder, from: "TenantUsers", to: "TenantMemberships");

        migrationBuilder.Sql("""CREATE VIEW "TenantUsers" AS SELECT "Id", "TenantId", "UserId", "AcceptedOn" FROM "TenantMemberships";""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP VIEW "TenantUsers";""");

        migrationBuilder.RenameTable(
            name: "TenantMemberships",
            newName: "TenantUsers");

        RenameKeysAndIndexes(migrationBuilder, from: "TenantMemberships", to: "TenantUsers");
    }

    private static void RenameKeysAndIndexes(MigrationBuilder migrationBuilder, string from, string to)
    {
        migrationBuilder.Sql($"""ALTER TABLE "{to}" RENAME CONSTRAINT "PK_{from}" TO "PK_{to}";""");
        migrationBuilder.Sql($"""ALTER TABLE "{to}" RENAME CONSTRAINT "FK_{from}_Tenants_TenantId" TO "FK_{to}_Tenants_TenantId";""");
        migrationBuilder.Sql($"""ALTER TABLE "{to}" RENAME CONSTRAINT "FK_{from}_Users_UserId" TO "FK_{to}_Users_UserId";""");

        migrationBuilder.RenameIndex(name: $"IX_{from}_TenantId_UserId", table: to, newName: $"IX_{to}_TenantId_UserId");
        migrationBuilder.RenameIndex(name: $"IX_{from}_TenantId_UserId_Accepted", table: to, newName: $"IX_{to}_TenantId_UserId_Accepted");
        migrationBuilder.RenameIndex(name: $"IX_{from}_UserId", table: to, newName: $"IX_{to}_UserId");
    }
}
