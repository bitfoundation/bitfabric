using Bunit;
using Boilerplate.Workbench.Components.Boards;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.WorkbenchTests.Features.Boards;

/// <summary>
/// The Submit bar of a board that asks for picks, rendered in memory against the running Workbench's review: Submit keeps the
/// picks in the board's file and wakes the wait of the agent whose task the board names, its words follow the board's
/// language, and only the user the agent works for sees it. Each test submits a board of its own, so its file is its own.
/// </summary>
[TestClass, TestCategory("UITest")]
public class BoardSubmitTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private readonly WorkbenchBoard board = WorkbenchBoardCatalog.Find("payments")! with { Id = $"test-{Guid.NewGuid():N}" };

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(WorkbenchBoardFiles.FilePath(board.Id)))
        {
            File.Delete(WorkbenchBoardFiles.FilePath(board.Id));
        }
    }

    private IRenderedComponent<CascadingValue<WorkbenchBoard>> Render(BunitContext ctx, string language = "en", string? progressTask = null, bool picks = true)
    {
        return ctx.Render<CascadingValue<WorkbenchBoard>>(p => p
            .Add(x => x.Value, board)
            .Add(x => x.IsFixed, true)
            .AddChildContent<BoardFrame>(frame => frame
                .Add(x => x.Title, "Taking payments")
                .Add(x => x.Language, language)
                .Add(x => x.Picks, picks)
                .Add(x => x.ProgressTask, progressTask)));
    }

    [TestMethod]
    public async Task Submit_Should_KeepThePicksInTheBoardsFileAndWakeTheWaitOfTheAgentItNames()
    {
        var server = await WorkbenchTestsInitializer.SharedServer;
        using var client = server.CreateClient();

        var created = await client.PostAsJsonAsync("api/progress", new { agent = "Claude Code", title = "Pick payment providers" }, TestContext.CancellationToken);
        var taskId = (string)JsonNode.Parse(await created.Content.ReadAsStringAsync(TestContext.CancellationToken))!["id"]!;

        await using var ctx = WorkbenchBunit.Create(server: server);
        var session = ctx.Services.GetRequiredService<WorkbenchReviewSession>();
        await session.Open("Sarah");
        await WorkbenchTestReviewer.WaitFor(() => Task.FromResult(session.Tasks), tasks => tasks.Any(t => t.Id == taskId));

        // The agent waits on its task, as it does for an answer.
        var waiting = client.GetStringAsync($"api/progress/{taskId}/wait?cursor=0&agent=Claude%20Code", TestContext.CancellationToken);

        var cut = Render(ctx, progressTask: taskId);

        cut.WaitForAssertion(() => Assert.AreEqual(WorkbenchStrings.BoardPicksSaved, cut.Find(".submit-status").TextContent));

        cut.Find(".board-submit button").Click();

        var events = JsonNode.Parse(await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken))!["events"]!.AsArray();

        Assert.HasCount(1, events);
        Assert.AreEqual("board", (string?)events[0]!["kind"]);
        Assert.AreEqual(board.Id, (string?)events[0]!["board"]);
        Assert.AreEqual(WorkbenchBoardFiles.FilePath(board.Id), (string?)events[0]!["file"]);
        Assert.AreEqual("Taking payments", (string?)events[0]!["text"]);

        var file = JsonNode.Parse(await File.ReadAllTextAsync(WorkbenchBoardFiles.FilePath(board.Id), TestContext.CancellationToken))!;

        Assert.IsTrue((bool)file["isSubmitted"]!);
        Assert.IsNotNull(file["submitted"]!["at"]);

        cut.WaitForAssertion(() => Assert.AreEqual(WorkbenchStrings.BoardSubmitted, cut.Find(".board-submit button").TextContent.Trim()));
        Assert.StartsWith(WorkbenchStrings.BoardPicksSubmittedAt[..^5], cut.Find(".submit-status").TextContent);

        // The user sees it in the task's activity, in the Workbench's language, with the board's title.
        var line = (await server.Services.GetRequiredService<WorkbenchProgressAgent>().GetTask(taskId, TestContext.CancellationToken))!.Feed.Single(e => e.Kind is WorkbenchProgressEntryKind.Board);

        Assert.AreEqual((board.Id, "Taking payments"), (line.RefId, line.Text));
    }

    [TestMethod]
    public async Task ABoardInPersian_Should_SayItsSubmitBarInPersianInAnEnglishWorkbench()
    {
        await using var ctx = WorkbenchBunit.Create();

        var cut = Render(ctx, language: "fa");
        var persian = CultureInfo.GetCultureInfo("fa");

        cut.WaitForAssertion(() => Assert.AreEqual(WorkbenchStrings.ResourceManager.GetString(nameof(WorkbenchStrings.BoardSubmit), persian), cut.Find(".board-submit button").TextContent.Trim()));
        Assert.AreEqual("ثبت", cut.Find(".board-submit button").TextContent.Trim());
        Assert.AreEqual(WorkbenchStrings.ResourceManager.GetString(nameof(WorkbenchStrings.BoardPicksSaved), persian), cut.Find(".submit-status").TextContent);
    }

    [TestMethod]
    public async Task TheSubmitBar_Should_ShowOnlyToTheUserTheAgentWorksForOnABoardThatAsksForPicks()
    {
        await using (var visitor = WorkbenchBunit.Create(isLocal: false))
        {
            Assert.IsEmpty(Render(visitor).FindAll(".board-submit"), "Someone with a tunnel's link submits nothing for the user.");
        }

        await using (var ctx = WorkbenchBunit.Create())
        {
            Assert.IsEmpty(Render(ctx, picks: false).FindAll(".board-submit"), "A board that asks for no picks has no bar.");
            Assert.HasCount(1, Render(ctx, progressTask: "any").FindAll(".board-submit"), "Naming the agent's task asks for picks.");

            // Rendered on its own, as the side panel's search renders a board to read it, it has no board to keep the picks of.
            Assert.IsEmpty(ctx.Render<BoardFrame>(p => p.Add(x => x.Title, "Taking payments").Add(x => x.Picks, true)).FindAll(".board-submit"));
        }
    }
}
