namespace Bit.Fabric.Server.Api.Features.Evaluation.Models;

/// <summary>
/// What is kept of one AI evaluation of bit Fabric (bitfabric-internal/specs/home-page.md, "The AI evaluation"): a summary
/// that names nothing of the visitor or their business, and whether it convinced them. The conversation itself is never kept.
/// </summary>
public partial class EvaluationOutcome
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public EvaluationSource Source { get; set; }

    /// <summary>What kind of business asked and what it needed, and what the verdict was, in a few anonymous lines.</summary>
    [Required, MaxLength(EvaluationRecorder.MaxSummaryLength)]
    public string? Summary { get; set; }

    public EvaluationConvinced Convinced { get; set; }

    /// <summary>The language the evaluation was held in, like <c>en</c> or <c>fa</c>.</summary>
    [MaxLength(16)]
    public string? Culture { get; set; }
}

/// <summary>Where an evaluation was held.</summary>
public enum EvaluationSource
{
    /// <summary>The chat panel, opened from the home page's fact-check band.</summary>
    Chat,

    /// <summary>The visitor's own AI, through /evaluate-mcp.</summary>
    Mcp
}

/// <summary>Whether the evaluation convinced the visitor that bit Fabric is worth starting with.</summary>
public enum EvaluationConvinced
{
    /// <summary>Not said, which is every evaluation through the visitor's own AI.</summary>
    Unknown,
    Yes,
    No
}

public partial class EvaluationOutcomeConfiguration : IEntityTypeConfiguration<EvaluationOutcome>
{
    public void Configure(EntityTypeBuilder<EvaluationOutcome> builder)
    {
        // Read newest first, by where they were held.
        builder.HasIndex(outcome => new { outcome.Source, outcome.CreatedOn });
    }
}
