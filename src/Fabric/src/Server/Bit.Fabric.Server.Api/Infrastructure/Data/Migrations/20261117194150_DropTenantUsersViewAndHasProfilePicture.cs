using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <summary>
/// The second of the two releases that take template 11.0.0-pre-21's new names: 0.0.13 kept the old ones next to the new for
/// 0.0.12, and Production runs 0.0.13 now, so they go. 0.0.13 keeps serving while this runs, and uses none of them (See
/// bitfabric-internal/specs/changes/2026-11-17-drop-old-names.md).
/// </summary>
public partial class DropTenantUsersViewAndHasProfilePicture : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The trigger goes first: PostgreSQL doesn't see that its function reads the old column, and would let the column go
        // while every insert and update of a user then fails.
        migrationBuilder.Sql("""
            DROP TRIGGER "Users_SyncHasProfileImage" ON "Users";
            DROP FUNCTION "Users_SyncHasProfileImage"();
            """);

        migrationBuilder.DropColumn(
            name: "HasProfilePicture",
            table: "Users");

        migrationBuilder.Sql("""DROP VIEW "TenantUsers";""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Back as 20261117152427_RenameTenantUsersToTenantMemberships and 20261117152826_AddHasProfileImageNextToHasProfilePicture
        // left them: the view, and the old column with its default, the new column's values and the trigger that keeps the two
        // equal.
        migrationBuilder.Sql("""CREATE VIEW "TenantUsers" AS SELECT "Id", "TenantId", "UserId", "AcceptedOn" FROM "TenantMemberships";""");

        migrationBuilder.AddColumn<bool>(
            name: "HasProfilePicture",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""UPDATE "Users" SET "HasProfilePicture" = "HasProfileImage";""");

        migrationBuilder.Sql("""
            CREATE FUNCTION "Users_SyncHasProfileImage"() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    NEW."HasProfileImage" := NEW."HasProfileImage" OR NEW."HasProfilePicture";
                ELSIF NEW."HasProfileImage" IS NOT DISTINCT FROM OLD."HasProfileImage"
                    AND NEW."HasProfilePicture" IS DISTINCT FROM OLD."HasProfilePicture" THEN
                    NEW."HasProfileImage" := NEW."HasProfilePicture";
                END IF;
                NEW."HasProfilePicture" := NEW."HasProfileImage";
                RETURN NEW;
            END $$;

            CREATE TRIGGER "Users_SyncHasProfileImage" BEFORE INSERT OR UPDATE ON "Users"
                FOR EACH ROW EXECUTE FUNCTION "Users_SyncHasProfileImage"();
            """);
    }
}
