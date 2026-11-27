using System.Security.Cryptography;
using System.Text;

namespace Boilerplate.Workbench.Components.Boards;

/// <summary>
/// The page states the screens of boards' flows show (BoardFlowScreen.State), each under an id made from the state itself, so
/// a screen's preview address stays the same however often its board renders. The preview reads one as a state saved with
/// the review (PreviewPage.SavedStatePrefix).
/// </summary>
public static class BoardFlowStates
{
    private static readonly ConcurrentDictionary<string, string> states = [];

    /// <summary>Keeps a state and returns its id: letters and digits only, as the saved states' ids are.</summary>
    public static string Add(string state)
    {
        var id = $"board{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state)))[..24]}";

        states[id] = state;

        return id;
    }

    public static string? Find(string id) => states.GetValueOrDefault(id);
}
