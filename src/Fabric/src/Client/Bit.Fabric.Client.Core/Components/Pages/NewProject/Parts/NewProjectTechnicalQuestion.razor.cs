using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// The choices that are technical: the database (Q33), with the license and limit warnings of MySQL and SQL Server, file
/// storage, working offline, the private GitHub repository and Cloudflare. File storage and Cloudflare are template options
/// moved here from "Super technical". Each has a recommended answer, so the designs fold them away.
/// </summary>
public partial class NewProjectTechnicalQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h3";

    private static NewProjectTemplateOption FilesStorage => NewProjectTemplateOption.Find("filesStorage")!;

    private bool IsCloudflareOn => Answers.Option("cloudflare") is "true";

    private void SwitchCloudflare(bool isOn) => Store.SetTemplateOption("cloudflare", isOn ? "true" : "false");

    private string DatabaseDescription(NewProjectDatabase database) => database switch
    {
        NewProjectDatabase.PostgreSql => Localizer[nameof(AppStrings.NewProjectPostgreSqlText)],
        NewProjectDatabase.Sqlite => Localizer[nameof(AppStrings.NewProjectSqliteText)],
        NewProjectDatabase.SqlServer => Localizer[nameof(AppStrings.NewProjectSqlServerText)],
        _ => Localizer[nameof(AppStrings.NewProjectMySqlText)]
    };
}
