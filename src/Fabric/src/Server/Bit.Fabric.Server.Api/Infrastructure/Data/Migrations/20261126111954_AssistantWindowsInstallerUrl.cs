using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <summary>
/// The seeded assistant prompt links the AdminPanel demo's Windows installer at its new address (template 11.0.0-pre-25).
/// Only the address changes, in every prompt that has it, so a prompt an admin has edited keeps the rest of its text.
/// </summary>
public partial class _20261126111954_AssistantWindowsInstallerUrl : Migration
{
    private const string OldInstallerUrl = "https://windows-admin.bitplatform.dev/AdminPanel.Client.Windows-win-Setup.exe";
    private const string NewInstallerUrl = "https://windows-adminpanel.bitfabric.co/AdminPanel.Client.Windows-win-Setup.exe";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""UPDATE "SystemPrompts" SET "Markdown" = replace("Markdown", '{OldInstallerUrl}', '{NewInstallerUrl}') WHERE strpos("Markdown", '{OldInstallerUrl}') > 0;""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""UPDATE "SystemPrompts" SET "Markdown" = replace("Markdown", '{NewInstallerUrl}', '{OldInstallerUrl}') WHERE strpos("Markdown", '{NewInstallerUrl}') > 0;""");
    }
}
