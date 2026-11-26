using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class _20261126194525_AddEvaluationOutcomes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EvaluationOutcomes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Source = table.Column<int>(type: "integer", nullable: false),
                Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Convinced = table.Column<int>(type: "integer", nullable: false),
                Culture = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EvaluationOutcomes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationOutcomes_Source_CreatedOn",
            table: "EvaluationOutcomes",
            columns: new[] { "Source", "CreatedOn" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EvaluationOutcomes");
    }
}
