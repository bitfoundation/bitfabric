namespace Bit.Fabric.Shared.Features.Growth.Dtos;

// The growth page's figures, every team's together, as totals only: counts per day of the viewer's time zone and a few
// totals of today, never a project, a team, an account or a name. GrowthReport turns them into the figures of a period.

/// <summary>Everything the growth page counts, from the first day there was anything to count to the last Trial's end.</summary>
public partial class GrowthDataDto
{
    /// <summary>Today, in the time zone the counts were asked for.</summary>
    public DateOnly Today { get; set; }

    /// <summary>Every day with something to count, oldest first, the days Trials still running will end on included.</summary>
    public List<GrowthDayDto> Days { get; set; } = [];

    /// <summary>
    /// Every project licensed after its Trial, by the day it was licensed and the days since its Trial began: how many
    /// projects each pair has.
    /// </summary>
    public List<GrowthLicenseDelayDto> LicenseDelays { get; set; } = [];

    /// <summary>Teams with a License by how many projects they licensed: one, two, three to five, six or more.</summary>
    public int[] TeamsByProjects { get; set; } = new int[4];

    /// <summary>The projects those teams licensed, each team's counted once per project.</summary>
    public int TeamProjects { get; set; }

    /// <summary>The people who are accepted members of a team with a License, each counted once.</summary>
    public int PeopleInLicensedTeams { get; set; }
}

/// <summary>One day's counts. A Trial bit CLI asked for from CI counts in none of them.</summary>
public partial class GrowthDayDto
{
    public DateOnly Day { get; set; }

    /// <summary>Projects bit Fabric first heard of that day: by their Trial, or by their first License when they had none.</summary>
    public int ProjectsMade { get; set; }

    /// <summary>Projects that got their first License that day.</summary>
    public int ProjectsLicensed { get; set; }

    /// <summary>Teams that got their first License that day.</summary>
    public int TeamsLicensed { get; set; }

    public int AccountsCreated { get; set; }

    /// <summary>Licensed projects whose production environment monitoring started watching that day, and still watches.</summary>
    public int InProduction { get; set; }

    /// <summary>Trials that end that day.</summary>
    public int TrialsEnded { get; set; }

    /// <summary>Of those, the ones whose project holds a License now.</summary>
    public int TrialsEndedLicensed { get; set; }

    /// <summary>
    /// Trials counted as converted on that day: the later of the Trial's end and the project's first License, so a share
    /// taken as of any day knows only what had happened by then.
    /// </summary>
    public int TrialsConverted { get; set; }
}

/// <summary>How many projects got their first License on <see cref="Day"/>, <see cref="Days"/> days after their Trial began.</summary>
public partial class GrowthLicenseDelayDto
{
    public DateOnly Day { get; set; }

    public int Days { get; set; }

    public int Count { get; set; }
}
