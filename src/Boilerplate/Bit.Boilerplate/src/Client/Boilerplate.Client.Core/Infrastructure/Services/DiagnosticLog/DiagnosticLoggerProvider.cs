//+:cnd:noEmit

namespace Boilerplate.Client.Core.Infrastructure.Services.DiagnosticLog;

// https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/developer-tools

/// <summary>
/// Provides a custom logger that keeps the recent log messages in an in memory store, from which the chatbot's
/// CheckLastError tool reads this device's last error (SharedAppMessages.UPLOAD_LAST_ERROR).
/// To read the logs in the app, open BevTools' Logs panel (See AppDevTools).
/// </summary>
[ProviderAlias("DiagnosticLogger")]
public partial class DiagnosticLoggerProvider(TimeProvider timeProvider) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new DiagnosticLogger(timeProvider)
        {
            Category = categoryName
        };
    }

    public void Dispose() { }
}
