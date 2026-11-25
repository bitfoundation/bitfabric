using System.Globalization;

namespace Boilerplate.Workbench.Components.Explainers;

/// <summary>
/// The frame of an explainer: a page an agent builds in the Workbench to explain something visually, such as an
/// architecture, a comparison of options, a plan or a report, in the app's own components and theme. It carries the
/// title, the lead, where and when the facts were read and a note above the content, and it reads in the language it is
/// written in, right to left for one that is, whatever language the view is in. See the explainer skill
/// (.github/agents/explainer.agent.md).
/// </summary>
public partial class ExplainerArticle
{
    private BitDir? dir;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>The short line above the title: the kind of explainer and its subject.</summary>
    [Parameter] public string? Eyebrow { get; set; }

    /// <summary>The eyebrow's icon; a light bulb when none is given.</summary>
    [Parameter] public string? IconName { get; set; }

    /// <summary>One or two sentences under the title: what the page answers, and for whom.</summary>
    [Parameter] public string? Lead { get; set; }

    /// <summary>Where and when its facts were read, such as "Prices as each provider published them on 25 November 2026".</summary>
    [Parameter] public string? Meta { get; set; }

    /// <summary>The language the explainer is written in, a culture name such as en or fa.</summary>
    [Parameter] public string Language { get; set; } = "en";

    /// <summary>What the reader must know before the rest, such as a caveat, shown above the sections.</summary>
    [Parameter] public RenderFragment? Note { get; set; }

    /// <summary>The explainer's sections (ExplainerSection) and its sources (ExplainerSources).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnParametersSet()
    {
        dir = IsRightToLeft(Language) ? BitDir.Rtl : BitDir.Ltr;

        base.OnParametersSet();
    }

    private static bool IsRightToLeft(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language).TextInfo.IsRightToLeft;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
