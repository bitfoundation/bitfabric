using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class _20261124073240_MoveAccountsToBitfabricCo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "store-user@bitfabric.co", "STORE-USER@BITFABRIC.CO" });

        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "store-admin@bitfabric.co", "STORE-ADMIN@BITFABRIC.CO" });

        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "test@bitfabric.co", "TEST@BITFABRIC.CO" });

        // Every other account on @bitplatform.dev moves to the same name on @bitfabric.co (the founder, 2026-11-24), unless
        // someone has that address already. A user name that is the address moves with it.
        migrationBuilder.Sql("""
            UPDATE "Users" u SET
                "Email" = regexp_replace(u."Email", '@bitplatform\.dev$', '@bitfabric.co', 'i'),
                "NormalizedEmail" = regexp_replace(u."NormalizedEmail", '@BITPLATFORM\.DEV$', '@BITFABRIC.CO'),
                "UserName" = regexp_replace(u."UserName", '@bitplatform\.dev$', '@bitfabric.co', 'i'),
                "NormalizedUserName" = regexp_replace(u."NormalizedUserName", '@BITPLATFORM\.DEV$', '@BITFABRIC.CO')
            WHERE u."NormalizedEmail" LIKE '%@BITPLATFORM.DEV'
              AND NOT EXISTS (SELECT 1 FROM "Users" o WHERE o."NormalizedEmail" = regexp_replace(u."NormalizedEmail", '@BITPLATFORM\.DEV$', '@BITFABRIC.CO'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Only the seeded accounts go back: another account on @bitfabric.co may have been there before Up.
        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "store-user@bitplatform.dev", "STORE-USER@BITPLATFORM.DEV" });

        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "store-admin@bitplatform.dev", "STORE-ADMIN@BITPLATFORM.DEV" });

        migrationBuilder.UpdateData(
            table: "Users",
            keyColumn: "Id",
            keyValue: new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"),
            columns: new[] { "Email", "NormalizedEmail" },
            values: new object[] { "test@bitplatform.dev", "TEST@BITPLATFORM.DEV" });
    }
}
