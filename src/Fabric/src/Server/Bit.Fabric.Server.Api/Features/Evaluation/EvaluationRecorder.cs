using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Evaluation.Models;

namespace Bit.Fabric.Server.Api.Features.Evaluation;

/// <summary>
/// Keeps the anonymous summary of an evaluation and whether it convinced the visitor. The model is asked to name nothing of
/// the visitor; a summary that still carries an email address, a phone number or a web address isn't kept at all.
/// </summary>
public partial class EvaluationRecorder(AppDbContext dbContext, TimeProvider timeProvider, ILogger<EvaluationRecorder> logger)
{
    public const int MaxSummaryLength = 1000;

    /// <summary>Keeps one outcome; returns false when the summary names something that identifies someone.</summary>
    public async Task<bool> Record(EvaluationSource source, string summary, EvaluationConvinced convinced, CancellationToken cancellationToken)
    {
        summary = summary.Trim();

        if (summary.Length is 0 || Identifies(summary))
        {
            logger.LogInformation("An evaluation's summary wasn't kept: it was empty or named an address, a phone number or a site.");
            return false;
        }

        await dbContext.EvaluationOutcomes.AddAsync(new()
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = timeProvider.GetUtcNow(),
            Source = source,
            Summary = summary.Length > MaxSummaryLength ? summary[..MaxSummaryLength] : summary,
            Convinced = convinced,
            Culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Whether the summary carries an email address, a phone number of seven digits or more, or a web address: any name with a
    /// dot before two or more lower-case letters, such as bloomflowers.biz. bit Fabric's own bitfabric.co, standing alone, names
    /// nobody; an email address or a subdomain on it still does. A library such as Node.js isn't a site.
    /// </summary>
    public static bool Identifies(string summary)
        => Email().IsMatch(summary)
           || Phone().Matches(summary).Any(match => match.Value.Count(char.IsAsciiDigit) >= 7)
           || Address().IsMatch(OwnSite().Replace(summary, "bit Fabric"));

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+?\d[\d\s().-]{5,}\d")]
    private static partial Regex Phone();

    // The ending in lower case, as an address is written, so ASP.NET isn't one.
    [GeneratedRegex(@"(?i:https?://|www\.)|\b[A-Za-z0-9][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)*\.(?!js\b)[a-z]{2,24}\b")]
    private static partial Regex Address();

    [GeneratedRegex(@"(?<![\w@.-])bitfabric\.co(?![\w.-]*\w)", RegexOptions.IgnoreCase)]
    private static partial Regex OwnSite();
}
