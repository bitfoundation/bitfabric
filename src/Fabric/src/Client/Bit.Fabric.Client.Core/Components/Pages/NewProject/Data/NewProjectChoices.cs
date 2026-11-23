using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>A real product or a quick try, the first question after the name (Q31).</summary>
public enum NewProjectGoal
{
    Real,
    Try
}

/// <summary>What the project starts with, the template's module option.</summary>
public enum NewProjectStart
{
    Blank,
    Admin,
    Store
}

/// <summary>The database, offered in the order the wizard recommends them (Q33).</summary>
public enum NewProjectDatabase
{
    PostgreSql,
    Sqlite,
    SqlServer,
    MySql
}

/// <summary>
/// Where the app's errors and logs go: the customer's own server, which costs nothing more, or a service of their own,
/// Sentry or Azure Application Insights, with its own account and its own bill.
/// </summary>
public enum NewProjectErrors
{
    OwnServer,
    Sentry,
    AppInsights
}

/// <summary>The visitor's computer, which decides the install line and which apps can be set up on it.</summary>
public enum NewProjectComputer
{
    Windows,
    MacOs,
    Linux
}

/// <summary>A native app to set up on this computer now; the web is always there (Q30).</summary>
public enum NewProjectDevice
{
    Android,
    Ios,
    Windows,
    MacOs
}
