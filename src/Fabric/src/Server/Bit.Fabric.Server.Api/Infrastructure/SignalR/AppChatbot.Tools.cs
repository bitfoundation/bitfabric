using System.ComponentModel;
using Bit.Fabric.Server.Api.Features.Identity;
using Bit.Fabric.Shared.Features.Chatbot;
using Bit.Fabric.Shared.Features.Diagnostic;
using Microsoft.Agents.AI;

namespace Bit.Fabric.Server.Api.Infrastructure.SignalR;

[McpServerToolType]
public partial class AppChatbot
{
    /// <summary>
    /// Returns the current date and time based on the user's timezone.
    /// </summary>
    [Description("Returns the current date and time based on the user's timezone.")]
    [McpServerTool(Name = nameof(GetCurrentDateTime))]
    private string GetCurrentDateTime([Required, Description("User's timezone id")] string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            var userDateTime = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);

            return $"Current date/time in user's timezone ({timeZoneId}) is {userDateTime:o}";
        }
        catch
        {
            return $"Current date/time in utc is {timeProvider.GetUtcNow():o}";
        }
    }

    /// <summary>
    /// Shows the user a form in the chat for leaving their contact details, so a human operator can follow up. The panel
    /// draws it from this call's arguments (See HumanFollowUpCard). No [McpServerTool]: the form needs the app's chat panel.
    /// </summary>
    [Description("Shows the user a form inside the chat for leaving their contact details, so a human operator can follow up on an issue you could not resolve. Use it instead of asking for an email address or phone number yourself.")]
    private async Task<string?> RequestHumanFollowUp(
        [Required, Description("A summary of the conversation so far in at most three sentences, written in the user's language")] string conversationSummary,
        CancellationToken cancellationToken = default)
    {
        var shown = await ui.Show(AiChatProtocol.Tools.RequestHumanFollowUp,
                                  arguments: JsonSerializer.SerializeToElement(new Dictionary<string, string?> { ["conversationSummary"] = conversationSummary }, AppJsonContext.Default.DictionaryStringString),
                                  shown: null,
                                  replay: $"Showed a form for leaving contact details, so a human operator can follow up on: {conversationSummary}",
                                  cancellationToken);

        return shown
            ? "The contact form was shown to the user in the chat. They fill in their contact details there, so do not ask for them yourself."
            : "Failed to show the contact form.";
    }

    /// <summary>Shown under the answer as prompts to tap. No [McpServerTool]: the buttons need the chat panel.</summary>
    [Description("Shows the user exactly 3 things they might want to ask or do next, as buttons to tap under your answer. Each is under 60 characters, worded as the user would say it and in the language you answer in.")]
    private async Task<string> ShowFollowUpSuggestions(
        [Required, Description("The suggestions, most useful first")] string[] suggestions,
        CancellationToken cancellationToken = default)
    {
        // More than this pushes the message box off the screen on a phone
        string[] shown = [.. suggestions.Where(suggestion => string.IsNullOrWhiteSpace(suggestion) is false).Select(suggestion => suggestion.Trim()).Take(3)];

        return await ui.Suggest(shown, cancellationToken)
            ? "The suggestions are shown to the user."
            : "Failed to show the suggestions.";
    }

    /// <summary>
    /// Navigates the user to a specific page within the application.
    /// </summary>
    [Description("Navigates the user to a specific page within the application. Use this tool only when the user explicitly requests to go to a particular section or feature of the app.")]
    private async Task<string?> NavigateToPage(
        [Required, Description("Page URL to navigate to")] string pageUrl,
        CancellationToken cancellationToken = default)
    {
        if (Uri.IsAppRelativeUrl(pageUrl) is false)
            return "Invalid page url. Only app relative urls such as /dashboard are allowed.";

        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            _ = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<bool>(SharedAppMessages.NAVIGATE_TO, pageUrl, cancellationToken);

            return "Navigation completed";
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return "Navigation failed";
        }
    }

    /// <summary>
    /// Returns the list of available application pages with their relative URLs and descriptions.
    /// </summary>
    [Description("Returns the list of available application pages, each with its relative URL and a short description. Call this tool whenever the user asks to find, open or navigate to a specific page/section of the app, then use the returned relative URL (e.g. /dashboard) with the NavigateToPage tool.")]
    [McpServerTool(Name = nameof(GetAppPages))]
    private object GetAppPages()
    {
        return PageUrls.GetPages();
    }

    /// <summary>One page of the site as Markdown (See Features.Evaluation.SiteContent).</summary>
    [Description("Reads one page of bitfabric.co as Markdown, by its address without the language, such as /features, /pricing or /compare/lovable; / is the home page. Call it when the site's pages aren't already in your instructions, as in a voice call, or to quote a page word for word.")]
    private async Task<string> ReadSitePage([Required, Description("The page's address, such as /features")] string page, CancellationToken cancellationToken)
    {
        return await siteContent.Page(httpContextAccessor.HttpContext!.Request.GetWebAppUrl(), page, cancellationToken)
            ?? "There is no such page, or it can't be read just now. GetAppPages lists the pages.";
    }

    /// <summary>Keeps the anonymous outcome of an evaluation opened from the home page (See Features.Evaluation.EvaluationRecorder).</summary>
    [Description(Features.Evaluation.EvaluationPrompt.RecordToolDescription)]
    private async Task<string> RecordEvaluationOutcome([Required, Description("The anonymous summary, two or three lines.")] string summary,
                                                       [Required, Description("Whether the visitor was convinced: yes or no")] string convinced,
                                                       CancellationToken cancellationToken)
    {
        if (isEvaluation is false)
            return "Only an evaluation opened from the home page's fact-check band is recorded: don't call this tool in this chat.";

        // A chatbot answers one question, so the chat's connection says what was kept already: one record per conversation.
        var recordedKey = $"EvaluationRecorded:{signalRConnectionId}";
        var cache = serviceProvider.GetRequiredService<IFusionCache>();

        if (isEvaluationRecorded || await cache.TryGetAsync<bool>(recordedKey, token: cancellationToken) is { HasValue: true })
            return "Already kept for this evaluation.";

        await using var scope = serviceProvider.CreateAsyncScope();

        var kept = await scope.ServiceProvider.GetRequiredService<Features.Evaluation.EvaluationRecorder>()
            .Record(Features.Evaluation.Models.EvaluationSource.Chat,
                    summary,
                    convinced.Trim().ToLowerInvariant() switch { "yes" or "true" => Features.Evaluation.Models.EvaluationConvinced.Yes, "no" or "false" => Features.Evaluation.Models.EvaluationConvinced.No, _ => Features.Evaluation.Models.EvaluationConvinced.Unknown },
                    cancellationToken);

        isEvaluationRecorded = kept;

        if (kept)
        {
            await cache.SetAsync(recordedKey, true, TimeSpan.FromDays(1), token: cancellationToken);
        }

        return kept ? "Kept. Don't mention it to the visitor." : "Not kept: it named something that identifies someone. Don't try again.";
    }

    [Description(@"Displays the sign-in modal to the user and waits for either successful sign-in or cancellation")]
    public async Task<UserDto?> ShowSignInModal(CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            var accessToken = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<string>(SharedAppMessages.SHOW_SIGN_IN_MODAL, cancellationToken);

            var bearerTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).BearerTokenProtector;
            var accessTokenTicket = bearerTokenProtector.Unprotect(accessToken);
            var user = accessTokenTicket!.Principal;

            return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Users
                .Project()
                .FirstOrDefaultAsync(u => u.Id == user.GetUserId());
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return null;
        }
    }

    /// <summary>
    /// Changes the user's culture/language setting.
    /// </summary>
    [Description("Changes the user's culture/language setting. Use this tool only when the user explicitly requests to change the app language. Common LCIDs: 1033=en-US, 1065=fa-IR, 1053=sv-SE, 2057=en-GB, 1043=nl-NL, 1081=hi-IN, 2052=zh-CN, 3082=es-ES, 1036=fr-FR, 1025=ar-SA, 1031=de-DE, 1030=da-DK.")]
    private async Task<string?> SetApplicationCulture(
        [Required, Description("Culture LCID (e.g., 1033 for en-US, 1065 for fa-IR)")] int cultureLcid,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureLcid);

            if (CultureInfoManager.SupportedCultures.All(c => c.Culture.LCID != cultureLcid))
                return $"The requested culture is not supported. Available cultures: {string.Join(", ", CultureInfoManager.SupportedCultures.Select(c => c.Culture.NativeName))}";

            _ = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<bool>(SharedAppMessages.CHANGE_CULTURE, cultureLcid, cancellationToken);

            return "Culture/Language changed successfully";
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return "Failed to change culture/language";
        }
    }

    /// <summary>
    /// Changes the user's theme preference between light and dark mode.
    /// </summary>
    [Description("Changes the user's theme preference between light and dark mode. Use this tool only when the user explicitly requests to change the app theme or appearance.")]
    private async Task<string?> SetApplicationTheme(
        [Required, Description("Theme name: 'light' or 'dark'")] string theme,
        CancellationToken cancellationToken = default)
    {
        if (theme != "light" && theme != "dark")
            return "Invalid theme. Use 'light' or 'dark'.";

        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            var themeChanged = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<bool>(SharedAppMessages.CHANGE_THEME, theme, cancellationToken);

            return themeChanged ? $"Theme changed to {theme} successfully" : $"Theme is already set to {theme}";
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return "Failed to change theme";
        }
    }

    /// <summary>
    /// Retrieves the last error that occurred on the user's device from the diagnostic logs.
    /// </summary>
    [Description("Retrieves the last error that occurred on the user's device from the diagnostic logs. Use this tool when troubleshooting user-reported issues, investigating application crashes, or when the user mentions something isn't working.")]
    private async Task<string?> CheckLastError(CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            var lastError = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<DiagnosticLogDto?>(SharedAppMessages.UPLOAD_LAST_ERROR, cancellationToken);

            if (lastError is null)
                return "No errors found in the diagnostic logs.";

            return lastError.ToString();
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return "Failed to retrieve error information from the device.";
        }
    }

    /// <summary>Clears application files on the user's device to fix issues, only once the user approves on screen (See <see cref="ChatbotUi.AskApproval"/>).</summary>
    [Description("Clears the app's files on the user's device - local data, cache and storage - to fix corrupted local state; it also signs the user out, deletes this conversation and restarts the app. It asks the user to approve on their screen and does nothing without that approval, so don't ask for permission in the conversation yourself.")]
    private async Task<string?> ClearAppFiles(CancellationToken cancellationToken = default)
    {
        var decision = await ui.AskApproval(AiChatProtocol.Tools.ClearAppFiles, localizer[nameof(AppStrings.AiChatApprovalClearAppFilesDescription)], cancellationToken);

        if (decision is BitAiApprovalDecision.Rejected)
            return "The user declined, so nothing was cleared. Carry on another way, and don't offer it again unless they bring it up.";

        if (decision is not (BitAiApprovalDecision.Approved or BitAiApprovalDecision.ApprovedAlways))
            return "The approval went unanswered, so nothing was cleared. Don't ask again on your own.";

        await using var scope = serviceProvider.CreateAsyncScope();

        try
        {
            var cleared = await scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>()
                .Clients.Client(signalRConnectionId!)
                .InvokeAsync<bool>(SharedAppMessages.CLEAR_APP_FILES, cancellationToken);

            return cleared
                ? "The user approved, and the app files are being cleared: the app signs out, deletes this conversation and restarts."
                : "Failed to clear app files on the device.";
        }
        catch (Exception exp)
        {
            serviceProvider.GetRequiredService<ApiServerExceptionHandler>().Handle(exp);
            return "Failed to clear app files on the device.";
        }
    }

}
