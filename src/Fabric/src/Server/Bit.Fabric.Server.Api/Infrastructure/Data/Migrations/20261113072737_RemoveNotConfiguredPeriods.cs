using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <summary>
/// Removes what a template from before <c>isNotConfigured</c> (bitfoundation/bitplatform#14963) left for a check that
/// only missed its settings, which it reported as Degraded with its stand-in's sentence: the periods, and the rollups of
/// the same check in the same time. Since then such a check is stored nowhere (bitfabric-internal/specs/monitoring.md, Not
/// configured is a setup finding). Test's three projects have 14 such periods, from 2026-11-12 23:34 to 2026-11-13 03:53;
/// Production has none. This is the only place a description's text is read, and it reads the template's own stand-in
/// sentence.
/// </summary>
public partial class RemoveNotConfiguredPeriods : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Status 1 is Degraded (See HealthCheckStatus).
        migrationBuilder.Sql("""
            DELETE FROM "ProjectRollups" AS r
            USING "ProjectCheckPeriods" AS p
            WHERE p."Status" = 1
              AND p."Description" LIKE '% is not configured. Configure it, or remove the code that uses it.'
              AND r."ProjectId" = p."ProjectId"
              AND r."CheckKey" = p."CheckKey"
              AND r."BucketStart" > p."StartedAt" - INTERVAL '15 minutes'
              AND r."BucketStart" < COALESCE(p."EndedAt", now());

            DELETE FROM "ProjectCheckPeriods"
            WHERE "Status" = 1
              AND "Description" LIKE '% is not configured. Configure it, or remove the code that uses it.';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // What was deleted wasn't the app's history, and isn't put back.
    }
}
