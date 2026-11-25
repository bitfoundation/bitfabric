namespace Boilerplate.Workbench.Components.Pages.Boards.Deploy;

/// <summary>
/// An example board (the boards skill, .github/agents/boards.agent.md): how this project gets from a change to its users,
/// drawn for the options it was created with (DeployFacts), in everyday words and in the Workbench's language. It has no
/// data to load and moves only where motion explains something: along the way to the users, and while the server switches
/// to a new version.
/// </summary>
public partial class DeployBoard
{
    private static readonly (string IconName, string Name, string WhatKey)[] Apps =
    [
        (BitIconName.WindowsLogo, "Windows", nameof(WorkbenchStrings.DeployAppWindows)),
        (BitIconName.CellPhone, "Android", nameof(WorkbenchStrings.DeployAppAndroid)),
        (BitIconName.MobileSelected, "iOS", nameof(WorkbenchStrings.DeployAppIos))
    ];

    private IReadOnlyList<BoardNumber> Numbers => [.. DeployFacts.Numbers.Select(number => number with { Label = Localizer[number.Label] })];

    /// <summary>The stops grouped by where they run, in order, for the brackets above them.</summary>
    private static IEnumerable<(DeployPlace Place, int Count)> Places
    {
        get
        {
            var stops = DeployFacts.Stops;

            for (var i = 0; i < stops.Length;)
            {
                var place = stops[i].Place;
                var count = 1;

                while (i + count < stops.Length && stops[i + count].Place == place)
                    count++;

                yield return (place, count);
                i += count;
            }
        }
    }

    private string PlaceName(DeployPlace place) => Localizer[place switch
    {
        DeployPlace.You => nameof(WorkbenchStrings.DeployPlaceYou),
        DeployPlace.GitHub => nameof(WorkbenchStrings.DeployPlaceGitHub),
        DeployPlace.Server => nameof(WorkbenchStrings.DeployPlaceServer),
        _ => nameof(WorkbenchStrings.DeployPlaceOnline)
    }];
}
