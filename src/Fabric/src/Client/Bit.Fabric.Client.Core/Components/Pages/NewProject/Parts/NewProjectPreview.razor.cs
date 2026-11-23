using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// A sketch of the visitor's own product that follows their answers: its name, the company switcher of multi-tenancy, the
/// dashboard or the store, the assistant and the bell, on a computer and a phone, and which apps get set up now. It is
/// decoration for the eye; the summary says the same in words.
/// </summary>
public partial class NewProjectPreview
{
    private static readonly int[] barHeights = [42, 58, 50, 72, 64, 86, 78];

    private static string Initial(NewProjectAnswers answers) => answers.DisplayName[..1].ToUpperInvariant();
}
