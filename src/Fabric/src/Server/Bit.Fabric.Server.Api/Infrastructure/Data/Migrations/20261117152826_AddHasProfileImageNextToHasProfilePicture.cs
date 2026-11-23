using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <summary>
/// Template 11.0.0-pre-21 renamed User.HasProfilePicture to HasProfileImage. The migrations run while the previous version
/// still serves, and it reads and writes "HasProfilePicture", so instead of a rename the new column comes next to the old one,
/// a trigger keeps the two equal whichever version writes, and a later release drops the old column and the trigger (See
/// bitfabric-internal/specs/workarounds.md).
/// </summary>
public partial class AddHasProfileImageNextToHasProfilePicture : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasProfileImage",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""UPDATE "Users" SET "HasProfileImage" = "HasProfilePicture";""");

        // This version doesn't know the old column, so its inserts leave it to a default.
        migrationBuilder.Sql("""ALTER TABLE "Users" ALTER COLUMN "HasProfilePicture" SET DEFAULT false;""");

        // An update copies whichever column changed into the other one, this version's first; a new user has an image if
        // either version says so.
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "Users_SyncHasProfileImage" ON "Users";
            DROP FUNCTION "Users_SyncHasProfileImage"();
            """);

        migrationBuilder.Sql("""ALTER TABLE "Users" ALTER COLUMN "HasProfilePicture" DROP DEFAULT;""");

        migrationBuilder.DropColumn(
            name: "HasProfileImage",
            table: "Users");
    }
}
