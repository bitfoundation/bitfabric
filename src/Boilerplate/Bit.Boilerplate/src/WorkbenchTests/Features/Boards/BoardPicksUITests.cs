namespace Boilerplate.WorkbenchTests.Features.Boards;

/// <summary>
/// The payments board asks which providers to set up, the example of a board with picks: what the reader ticks is in the
/// board's file as they go, Submit keeps it there as final, a change afterwards asks to submit again, and a reload comes back
/// as it was left. Not in parallel: every test that changes the payments board writes the same file.
/// </summary>
[TestClass, TestCategory("UITest"), DoNotParallelize]
public class BoardPicksUITests : WorkbenchPageTest
{
    private static string FilePath => WorkbenchBoardFiles.FilePath("payments");

    private ILocator Pick(string provider) => Page.GetByRole(AriaRole.Checkbox, new() { Name = $"Set up {provider}", Exact = true });

    /// <summary>What the reader presses to pick a provider: its checkbox's label, which covers the box.</summary>
    private ILocator PickLabel(string provider) => Page.Locator("#providers label", new() { HasTextRegex = new Regex($"^Set up {provider}$") });

    private ILocator SubmitBar => Page.GetByRole(AriaRole.Region, new() { Name = WorkbenchStrings.BoardSubmitRegion, Exact = true });

    /// <summary>The board's file once it says what the condition waits for.</summary>
    private static Task<JsonNode> File(Func<JsonNode, bool> condition) => WorkbenchTestReviewer.WaitFor(
        async () => System.IO.File.Exists(FilePath) ? JsonNode.Parse(await System.IO.File.ReadAllTextAsync(FilePath)) ?? new JsonObject() : new JsonObject(),
        condition);

    private static string? Providers(JsonNode? state) => state?["Providers"]?.ToJsonString();

    [TestMethod, WithAnAgent]
    public async Task ThePicks_Should_ReachTheBoardsFileAsTheyAreMadeAndStayAsSubmitted(WorkbenchSurface surface)
    {
        if (System.IO.File.Exists(FilePath))
        {
            System.IO.File.Delete(FilePath);
        }

        await Page.GotoAsync(new Uri(Server.Address, "boards/payments").ToString());
        await WaitUntilInteractive();

        await Expect(SubmitBar.GetByRole(AriaRole.Status)).ToHaveTextAsync(WorkbenchStrings.BoardPicksSaved);

        await PickLabel("Mollie").ScrollIntoViewIfNeededAsync();
        await Press(PickLabel("Mollie"));
        await Press(PickLabel("Paddle"));

        await File(file => Providers(file["state"]) == """["Mollie","Paddle"]""");

        // Submit stays at hand at the foot of the screen while the board scrolls under it.
        await Press(SubmitBar.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.BoardSubmit, Exact = true }));

        await Expect(SubmitBar.GetByRole(AriaRole.Button)).ToHaveTextAsync(WorkbenchStrings.BoardSubmitted);

        var submitted = await File(file => file["isSubmitted"]?.GetValue<bool>() is true);

        Assert.AreEqual("""["Mollie","Paddle"]""", Providers(submitted["submitted"]?["state"]));

        // A change afterwards keeps what was sent, and asks to submit again.
        await Press(PickLabel("Paddle"));

        await Expect(SubmitBar.GetByRole(AriaRole.Button)).ToHaveTextAsync(WorkbenchStrings.BoardSubmitAgain);

        var changed = await File(file => Providers(file["state"]) == """["Mollie"]""");

        Assert.IsFalse(changed["isSubmitted"]!.GetValue<bool>());
        Assert.AreEqual("""["Mollie","Paddle"]""", Providers(changed["submitted"]?["state"]));

        // A reload comes back as it was left, the bar included.
        await Page.ReloadAsync();
        await WaitUntilInteractive();

        await Expect(Pick("Mollie")).ToBeCheckedAsync();
        await Expect(Pick("Paddle")).Not.ToBeCheckedAsync();
        await Expect(SubmitBar.GetByRole(AriaRole.Button)).ToHaveTextAsync(WorkbenchStrings.BoardSubmitAgain);
    }
}
