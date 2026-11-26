using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class _20261125222123_ProbeVisitorTime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "VisitorMs",
            table: "ProjectProbes",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VisitorMs",
            table: "ProjectProbes");
    }
}
