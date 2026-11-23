using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddNetworkLocations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string[]>(
            name: "Locations",
            table: "Projects",
            type: "text[]",
            nullable: false,
            defaultValue: new string[0]);

        // The projects already watched get the locations a new project gets (NetworkLocations.Defaults, as of this migration).
        migrationBuilder.Sql("""UPDATE "Projects" SET "Locations" = ARRAY['enam', 'oc']""");

        migrationBuilder.AddColumn<string>(
            name: "Colo",
            table: "ProjectProbes",
            type: "character varying(8)",
            maxLength: 8,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Locations",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "Colo",
            table: "ProjectProbes");
    }
}
