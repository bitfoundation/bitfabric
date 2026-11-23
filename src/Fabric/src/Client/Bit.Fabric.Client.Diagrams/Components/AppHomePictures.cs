using static Bit.Fabric.Client.Diagrams.Components.AppHomePictureKind;
using static Bit.Fabric.Client.Diagrams.Components.AppHomePictureTone;

namespace Bit.Fabric.Client.Diagrams.Components;

/// <summary>
/// The home page's pictures, written for someone who isn't technical: "No gap" and "Two doors", picked from the three of
/// each compared in the Workbench (bitfabric-internal/specs/home-page.md, 2026-11-09), and "Relay" for how the AI builds a
/// feature, picked from three. HomePage repeats their words as a plain list until they're loaded.
/// </summary>
public sealed class AppHomePictures(IStringLocalizer<AppStrings> localizer)
{
    private static AppHomePictureArea At(double x, double y, double width, double height) => new(x, y, width, height);

    public AppHomePicture GoingLive() => new("going-live", localizer[nameof(AppStrings.AppHomeGoingLivePicture)], 370, 640,
    [
        new("served", BitIconName.People, localizer[nameof(AppStrings.AppHomeGoingLiveServed)], localizer[nameof(AppStrings.AppHomeGoingLiveServedText)], At(0, 0, 1040, 72), At(0, 0, 320, 96)) { Kind = Bar, Tone = Good, Step = 0 },
        new("old", BitIconName.ThisPC, localizer[nameof(AppStrings.AppHomeGoingLiveOld)], null, At(0, 112, 640, 60), At(0, 136, 320, 60)) { Kind = Bar },
        new("data", BitIconName.Database, localizer[nameof(AppStrings.AppHomeGoingLiveData)], null, At(0, 212, 300, 60), At(0, 236, 320, 60)) { Kind = Bar, Step = 1 },
        new("new", BitIconName.Rocket, localizer[nameof(AppStrings.AppHomeGoingLiveNew)], null, At(340, 212, 700, 60), At(0, 336, 320, 60)) { Kind = Bar, Tone = Good, Step = 2 },
        new("both", BitIconName.Sync, localizer[nameof(AppStrings.AppHomeGoingLiveBoth)], null, At(340, 300, 300, 56), At(0, 436, 320, 60)) { Kind = Chip, Step = 3 },
        new("gone", BitIconName.CheckMark, localizer[nameof(AppStrings.AppHomeGoingLiveGone)], null, At(740, 300, 300, 56), At(0, 536, 320, 60)) { Kind = Chip, Tone = Good, Step = 4 }
    ],
    [
        new("data", "new") { Flows = true },
        new("new", "both") { WideFrom = "b", WideTo = "t", NarrowFrom = "b", NarrowTo = "t" },
        new("both", "gone") { Flows = true, Tone = Good }
    ]);

    public AppHomePicture Protection() => new("protection", localizer[nameof(AppStrings.AppHomeProtectionPicture)], 440, 1070,
    [
        new("anyone", BitIconName.Warning, localizer[nameof(AppStrings.AppHomeProtectionAnyone)], localizer[nameof(AppStrings.AppHomeProtectionAnyoneText)], At(0, 150, 200, 118), At(0, 0, 320, 96)) { Tone = Bad },
        new("visitors", BitIconName.People, localizer[nameof(AppStrings.AppHomeProtectionVisitors)], null, At(0, 20, 200, 72), At(0, 136, 320, 64)) { Step = 0 },
        new("front", BitIconName.Shield, localizer[nameof(AppStrings.AppHomeProtectionFront)], localizer[nameof(AppStrings.AppHomeProtectionFrontText)], At(290, 0, 240, 112), At(0, 236, 320, 104)) { Tone = Good, Step = 1 },
        new("server", null, localizer[nameof(AppStrings.AppHomeProtectionServer)], null, At(640, 0, 400, 420), At(0, 380, 320, 300)) { Kind = Room },
        new("website", BitIconName.Globe, localizer[nameof(AppStrings.AppHomeProtectionWebsite)], null, At(32, 56, 336, 64), At(16, 44, 288, 64)) { Kind = Chip, ParentId = "server", Step = 2 },
        new("database", BitIconName.Database, localizer[nameof(AppStrings.AppHomeProtectionData)], null, At(32, 182, 336, 64), At(16, 132, 288, 64)) { Kind = Chip, ParentId = "server" },
        new("dashboards", BitIconName.BarChartVerticalFill, localizer[nameof(AppStrings.AppHomeProtectionDashboards)], null, At(32, 308, 336, 64), At(16, 220, 288, 64)) { Kind = Chip, ParentId = "server", Step = 4 },
        new("staff", BitIconName.Lock, localizer[nameof(AppStrings.AppHomeProtectionStaff)], localizer[nameof(AppStrings.AppHomeProtectionStaffText)], At(290, 300, 240, 120), At(0, 720, 320, 112)) { Tone = Good, Step = 3 },
        new("team", BitIconName.Contact, localizer[nameof(AppStrings.AppHomeProtectionTeam)], null, At(0, 328, 200, 72), At(0, 872, 320, 64)) { Step = 2 }
    ],
    [
        new("visitors", "front") { Flows = true },
        new("front", "website") { Flows = true, Tone = Good },
        new("website", "database") { WideFrom = "b", WideTo = "t" },
        new("dashboards", "database") { WideFrom = "t", WideTo = "b", NarrowFrom = "t", NarrowTo = "b" },
        new("team", "staff") { Flows = true, NarrowFrom = "t", NarrowTo = "b" },
        new("staff", "dashboards") { Flows = true, Tone = Good, NarrowFrom = "t", NarrowTo = "b" },
        new("anyone", "server") { Tone = Bad, Dashed = true, Label = localizer[nameof(AppStrings.AppHomeProtectionNoWayIn)], WideFrom = "r", WideTo = "l", NarrowFrom = "r", NarrowTo = "r" }
    ]);

    /// <summary>
    /// How the AI builds a feature (AI-DLC): the steps one after another, like a relay, with the visitor's approval where the
    /// design hands over to the code. The Workbench is named in the design's title, so its line has room for every state,
    /// and the visitor gives feedback there rather than only seeing it.
    /// </summary>
    public AppHomePicture AiDlc() => new("ai-dlc", localizer[nameof(AppStrings.AppHomeAiDlcPicture)], 380, 1130,
    [
        new("ask", BitIconName.Contact, localizer[nameof(AppStrings.AppHomeAiDlcAsk)], localizer[nameof(AppStrings.AppHomeAiDlcAskText)], At(0, 0, 180, 132), At(0, 0, 320, 104)) { Step = 0 },
        new("questions", BitIconName.Chat, localizer[nameof(AppStrings.AppHomeAiDlcQuestions)], localizer[nameof(AppStrings.AppHomeAiDlcQuestionsText)], At(230, 0, 240, 132), At(0, 140, 320, 104)) { Step = 1 },
        new("plan", BitIconName.Edit, localizer[nameof(AppStrings.AppHomeAiDlcPlan)], localizer[nameof(AppStrings.AppHomeAiDlcPlanText)], At(520, 0, 220, 132), At(0, 280, 320, 104)) { Step = 2 },
        new("design", BitIconName.Color, localizer[nameof(AppStrings.AppHomeAiDlcDesign)], localizer[nameof(AppStrings.AppHomeAiDlcDesignText)], At(790, 0, 250, 132), At(0, 420, 320, 124)) { Step = 3 },
        new("build", BitIconName.Code, localizer[nameof(AppStrings.AppHomeAiDlcBuild)], localizer[nameof(AppStrings.AppHomeAiDlcBuildText)], At(0, 240, 240, 132), At(0, 580, 320, 104)) { Step = 4 },
        new("tests", BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeAiDlcTests)], localizer[nameof(AppStrings.AppHomeAiDlcTestsText)], At(290, 240, 240, 132), At(0, 720, 320, 104)) { Step = 5 },
        new("review", BitIconName.Search, localizer[nameof(AppStrings.AppHomeAiDlcReview)], localizer[nameof(AppStrings.AppHomeAiDlcReviewText)], At(580, 240, 220, 132), At(0, 860, 320, 104)) { Step = 6 },
        new("ready", BitIconName.CheckMark, localizer[nameof(AppStrings.AppHomeAiDlcReady)], localizer[nameof(AppStrings.AppHomeAiDlcReadyText)], At(850, 240, 190, 132), At(0, 1000, 320, 104)) { Tone = Good, Step = 7 }
    ],
    [
        new("ask", "questions") { Flows = true },
        new("questions", "plan") { Flows = true },
        new("plan", "design") { Flows = true },
        new("design", "build") { Flows = true, Tone = Good, Label = localizer[nameof(AppStrings.AppHomeAiDlcYouApprove)], WideFrom = "b", WideTo = "t" },
        new("build", "tests") { Flows = true },
        new("tests", "review") { Flows = true },
        new("review", "ready") { Flows = true, Tone = Good }
    ]);
}
