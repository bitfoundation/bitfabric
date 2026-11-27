using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boilerplate.WorkbenchTests.Features.Boards;

/// <summary>
/// What a board's reader picks reaches a file of the board's, where the agent that built it reads it without the chat: the
/// file's shape, what a submit keeps, and which saves write it, only the Workbench's own window's and only for the user the
/// agent works for. Each test has a board file of its own: a made-up board for the file, the deploy board, whose own store
/// nothing else saves, for the storage.
/// </summary>
[TestClass, TestCategory("UnitTest"), DoNotParallelize]
public class BoardFilesTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private readonly string boardId = $"test-{Guid.NewGuid():N}";
    private readonly WorkbenchBoardFiles files = new(NullLogger<WorkbenchBoardFiles>.Instance);

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in new[] { WorkbenchBoardFiles.FilePath(boardId), WorkbenchBoardFiles.FilePath("deploy") }.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private static JsonNode ReadFile(string boardId) => JsonNode.Parse(File.ReadAllText(WorkbenchBoardFiles.FilePath(boardId)))!;

    [TestMethod]
    public async Task ABoardsFile_Should_HoldItsStoreAsSavedAndThePicksAsSubmitted()
    {
        Assert.IsNull(await files.Read(boardId, TestContext.CancellationToken), "Nothing is saved before the store first saves.");

        // A store with a version of its own is saved in Blex's envelope; the file holds the store itself and its version.
        await files.Save(boardId, """{"__blexStoreVersion":2,"state":{"Picked":["Search","Teams"]}}""", TestContext.CancellationToken);

        var saved = ReadFile(boardId);

        Assert.AreEqual(boardId, (string?)saved["board"]);
        Assert.IsFalse((bool)saved["isSubmitted"]!);
        Assert.IsNull(saved["submitted"]);
        Assert.AreEqual(2, (int)saved["storeVersion"]!);
        Assert.AreEqual("""["Search","Teams"]""", saved["state"]!["Picked"]!.ToJsonString());

        var submitted = await files.Submit(boardId, JsonNode.Parse("""{"Picked":["Search","Teams"]}""")!.AsObject(), 2, "Sarah", TestContext.CancellationToken);

        Assert.IsNotNull(submitted);
        Assert.IsTrue(submitted.IsSubmitted);

        var afterSubmit = ReadFile(boardId);

        Assert.IsTrue((bool)afterSubmit["isSubmitted"]!);
        Assert.AreEqual("Sarah", (string?)afterSubmit["submitted"]!["by"]);
        Assert.AreEqual("""["Search","Teams"]""", afterSubmit["submitted"]!["state"]!["Picked"]!.ToJsonString());

        // A change afterwards leaves the picks as they were sent, and says they changed since.
        await files.Save(boardId, """{"__blexStoreVersion":2,"state":{"Picked":["Search"]}}""", TestContext.CancellationToken);

        var changed = ReadFile(boardId);

        Assert.IsFalse((bool)changed["isSubmitted"]!);
        Assert.AreEqual("""["Search"]""", changed["state"]!["Picked"]!.ToJsonString());
        Assert.AreEqual("""["Search","Teams"]""", changed["submitted"]!["state"]!["Picked"]!.ToJsonString());

        // A save that changes nothing leaves the file as it is.
        var before = File.ReadAllText(WorkbenchBoardFiles.FilePath(boardId));

        await files.Save(boardId, """{"__blexStoreVersion":2,"state":{"Picked":["Search"]}}""", TestContext.CancellationToken);

        Assert.AreEqual(before, File.ReadAllText(WorkbenchBoardFiles.FilePath(boardId)));
    }

    [TestMethod]
    public async Task APersianBoardsFile_Should_KeepItsWordsAsTheyAre()
    {
        await files.Save(boardId, """{"Picked":["پرداخت با کارت"]}""", TestContext.CancellationToken);

        Assert.Contains("پرداخت با کارت", File.ReadAllText(WorkbenchBoardFiles.FilePath(boardId)));
    }

    [TestMethod]
    public void OnlyTheStoreOfABoardTheCatalogHas_Should_GetAFile()
    {
        Assert.AreEqual("payments", WorkbenchBoardFiles.BoardOf("blex:board-payments"));
        Assert.IsNull(WorkbenchBoardFiles.BoardOf("blex:board-nothing-here"), "No board has this id.");
        Assert.IsNull(WorkbenchBoardFiles.BoardOf("blex:workbench"), "Not a board's store.");
        Assert.IsNull(WorkbenchBoardFiles.BoardOf("blex:board-payments:2"), "Another instance of a scoped store.");
    }

    [TestMethod]
    public async Task ASaveInTheWorkbenchsOwnWindow_Should_ReachTheBoardsFile()
    {
        await using var ctx = WorkbenchBunit.Create();

        await Storage(ctx).SetAsync("blex:board-deploy", """{"Picked":["Test"]}""", TestContext.CancellationToken);

        Assert.AreEqual("""["Test"]""", ReadFile("deploy")["state"]!["Picked"]!.ToJsonString());

        await Storage(ctx).RemoveAsync("blex:board-deploy", TestContext.CancellationToken);

        Assert.IsFalse(File.Exists(WorkbenchBoardFiles.FilePath("deploy")), "A store whose saved state is dropped takes its file with it.");
    }

    [TestMethod]
    public async Task ASaveOfAViewsPreviewOrACapture_Should_NeverReachTheBoardsFile()
    {
        await using var ctx = WorkbenchBunit.Create();

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"/preview/deploy?{WorkbenchUrls.PaneQueryKey}=1");

        await Storage(ctx).SetAsync("blex:board-deploy", """{"Picked":["Test"]}""", TestContext.CancellationToken);

        Assert.IsFalse(File.Exists(WorkbenchBoardFiles.FilePath("deploy")));

        await using var capture = WorkbenchBunit.Create();

        capture.Services.GetRequiredService<NavigationManager>().NavigateTo($"/boards/deploy?{WorkbenchUrls.SnapshotQueryKey}=1");

        await Storage(capture).SetAsync("blex:board-deploy", """{"Picked":["Test"]}""", TestContext.CancellationToken);

        Assert.IsFalse(File.Exists(WorkbenchBoardFiles.FilePath("deploy")));
    }

    [TestMethod]
    public async Task AVisitorsSave_Should_NeverReachTheBoardsFile()
    {
        await using var ctx = WorkbenchBunit.Create(isLocal: false);

        await Storage(ctx).SetAsync("blex:board-deploy", """{"Picked":["Test"]}""", TestContext.CancellationToken);

        Assert.IsFalse(File.Exists(WorkbenchBoardFiles.FilePath("deploy")), "Someone with a tunnel's link never changes the user's picks.");
    }

    /// <summary>The Workbench's own storage, which the bUnit context otherwise replaces with one in memory.</summary>
    private static WorkbenchStateStorage Storage(BunitContext ctx) => ActivatorUtilities.CreateInstance<WorkbenchStateStorage>(ctx.Services);
}
