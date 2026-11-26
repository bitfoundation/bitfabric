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

        // bit Fabric's own address names nobody.
        if (summary.Length is 0 || Identifying().IsMatch(summary.Replace("bitfabric.co", "bit Fabric", StringComparison.OrdinalIgnoreCase)))
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

    // An email address, a phone number (seven digits or more, with the usual separators) or a web address.
    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+|\+?\d[\d\s().-]{6,}\d|https?://|www\.|\b[\w-]+\.(com|net|org|io|co|nl|ir|de|uk|app|dev|ai)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Identifying();
}
