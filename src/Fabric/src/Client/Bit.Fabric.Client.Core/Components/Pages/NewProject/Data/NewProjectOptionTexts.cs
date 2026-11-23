namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>
/// What the page says about the template's options (See <see cref="NewProjectTemplateOption"/>), shared by the technical
/// choices and "Super technical", each option by its name in bit new.
/// </summary>
public static class NewProjectOptionTexts
{
    /// <summary>The title of an option, or of one of the sections' own parts.</summary>
    public static string Title(IStringLocalizer localizer, string name) => name switch
    {
        "default" => localizer[nameof(AppStrings.NewProjectOptionDefault)],
        "builtIn" => localizer[nameof(AppStrings.NewProjectOptionBuiltIn)],
        "framework" => localizer[nameof(AppStrings.NewProjectOptionFramework)],
        "theme" => localizer[nameof(AppStrings.NewProjectOptionTheme)],
        "api" => localizer[nameof(AppStrings.NewProjectOptionApi)],
        "filesStorage" => localizer[nameof(AppStrings.NewProjectOptionFilesStorage)],
        "captcha" => localizer[nameof(AppStrings.NewProjectOptionCaptcha)],
        "aspire" => "Aspire",
        "redis" => "Redis",
        "cloudflare" => "Cloudflare",
        "brouter" => "bit Brouter",
        "ads" => "Google Ads",
        "advancedTests" => localizer[nameof(AppStrings.NewProjectOptionAdvancedTests)],
        _ => name
    };

    public static string? Lead(IStringLocalizer localizer, string name) => name switch
    {
        "intro" => localizer[nameof(AppStrings.NewProjectOptionIntroLead)],
        "builtIn" => localizer[nameof(AppStrings.NewProjectOptionBuiltInLead)],
        "framework" => localizer[nameof(AppStrings.NewProjectOptionFrameworkLead)],
        "theme" => localizer[nameof(AppStrings.NewProjectOptionThemeLead)],
        "api" => localizer[nameof(AppStrings.NewProjectOptionApiLead)],
        "filesStorage" => localizer[nameof(AppStrings.NewProjectOptionFilesStorageLead)],
        "captcha" => localizer[nameof(AppStrings.NewProjectOptionCaptchaLead)],
        "aspire" => localizer[nameof(AppStrings.NewProjectOptionAspireLead)],
        "redis" => localizer[nameof(AppStrings.NewProjectOptionRedisLead)],
        "cloudflare" => localizer[nameof(AppStrings.NewProjectOptionCloudflareLead)],
        "brouter" => localizer[nameof(AppStrings.NewProjectOptionBrouterLead)],
        "ads" => localizer[nameof(AppStrings.NewProjectOptionAdsLead)],
        "advancedTests" => localizer[nameof(AppStrings.NewProjectOptionAdvancedTestsLead)],
        _ => (string?)null
    };

    public static string ChoiceText(IStringLocalizer localizer, string name, string value) => (name, value) switch
    {
        ("framework", "net10.0") => ".NET 10",
        ("framework", _) => ".NET 11",
        ("theme", "Fluent2") => "Fluent 2",
        ("api", "Integrated") => localizer[nameof(AppStrings.NewProjectOptionApiIntegrated)],
        ("api", _) => localizer[nameof(AppStrings.NewProjectOptionApiStandalone)],
        ("filesStorage", "Local") => localizer[nameof(AppStrings.NewProjectOptionStorageLocal)],
        ("filesStorage", "S3") => localizer[nameof(AppStrings.NewProjectOptionStorageS3)],
        ("filesStorage", "AzureBlobStorage") => "Azure Blob Storage",
        ("filesStorage", _) => localizer[nameof(AppStrings.NewProjectOptionStorageOther)],
        ("captcha", "reCaptcha") => "Google reCAPTCHA",
        ("captcha", _) => localizer[nameof(AppStrings.NewProjectOptionNone)],
        _ => value
    };

    public static string? ChoiceDescription(IStringLocalizer localizer, string name, string? value) => (name, value) switch
    {
        ("framework", "net10.0") => localizer[nameof(AppStrings.NewProjectOptionNet10Text)],
        ("framework", _) => localizer[nameof(AppStrings.NewProjectOptionNet11Text)],
        ("theme", "Cupertino") or ("theme", "Material") => localizer[nameof(AppStrings.NewProjectOptionExperimental)],
        ("api", "Integrated") => localizer[nameof(AppStrings.NewProjectOptionApiIntegratedText)],
        ("api", _) => localizer[nameof(AppStrings.NewProjectOptionApiStandaloneText)],
        ("filesStorage", "Local") => localizer[nameof(AppStrings.NewProjectOptionStorageLocalText)],
        ("filesStorage", "S3") => localizer[nameof(AppStrings.NewProjectOptionStorageS3Text)],
        ("filesStorage", "AzureBlobStorage") => localizer[nameof(AppStrings.NewProjectOptionStorageAzureText)],
        ("filesStorage", _) => localizer[nameof(AppStrings.NewProjectOptionStorageOtherText)],
        ("captcha", "None") => localizer[nameof(AppStrings.NewProjectOptionCaptchaNoneText)],
        _ => (string?)null
    };
}
