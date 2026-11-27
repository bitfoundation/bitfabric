using Bit.Bcrdt;

namespace Boilerplate.WorkbenchTests.Features.Review;

/// <summary>
/// The review document is shared by every browser and the agent, and kept on disk, so what it reads back and what it
/// trims decide what each of them sees.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class WorkbenchReviewDocumentTests
{
    private static readonly WorkbenchReviewAnchor anchor = new("div.card > button", "BitButton", "Save", 0.25, 0.75);

    private readonly BcrdtDocument document = new();

    private BcrdtMap Threads => document.GetMap("threads");

    private void Edit(Action edit) => document.Transact(_ => edit());

    private WorkbenchReviewThread Thread(string id) => WorkbenchReviewDocument.ReadThreads(Threads).Single(t => t.Id == id);

    [TestMethod]
    public void AThread_Should_ReadBackAsItWasPinned()
    {
        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "buildings", "?theme=dark", anchor, "Sarah", "Make it bigger", hasState: true));

        var thread = Thread("a");

        Assert.AreEqual("buildings", thread.PageKey);
        Assert.AreEqual("?theme=dark", thread.Query);
        Assert.AreEqual(WorkbenchThemeMode.Dark, thread.Scenario.Theme);
        Assert.AreEqual(anchor, thread.Anchor);
        Assert.AreEqual(WorkbenchReviewStatus.Open, thread.Status);
        Assert.IsTrue(thread.HasState);
        Assert.IsGreaterThan(0, thread.Sequence);
        Assert.HasCount(1, thread.Messages);
        Assert.IsFalse(thread.Messages[0].FromAgent);
        Assert.AreEqual(("Sarah", "Make it bigger"), (thread.Messages[0].Author, thread.Messages[0].Text));
        Assert.IsEmpty(thread.Tasks);
    }

    [TestMethod]
    public void Threads_Should_BeNumberedPerPageInTheOrderTheyWereMade()
    {
        Edit(() =>
        {
            WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor, "R", "first", false);
            WorkbenchReviewDocument.AddThread(Threads, "b", "buildings", "", anchor, "R", "second", false);
            WorkbenchReviewDocument.AddThread(Threads, "c", "theme", "", anchor, "R", "third", false);
        });

        Assert.AreEqual(1, Thread("a").Number);
        Assert.AreEqual(1, Thread("b").Number);
        Assert.AreEqual(2, Thread("c").Number);
    }

    [TestMethod]
    public void TheTexts_Should_BeTrimmedToTheirLimits()
    {
        var longText = $"  {new string('t', WorkbenchReviewDocument.MaxTextLength + 100)}  ";
        var longName = new string('n', WorkbenchReviewDocument.MaxNameLength + 10);

        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor with { Text = new string('x', 500), X = -3, Y = 9 }, longName, longText, false));

        var thread = Thread("a");

        Assert.AreEqual(WorkbenchReviewDocument.MaxTextLength, thread.Messages[0].Text.Length);
        Assert.AreEqual(WorkbenchReviewDocument.MaxNameLength, thread.Messages[0].Author.Length);
        Assert.AreEqual(120, thread.Anchor.Text!.Length);
        Assert.AreEqual((0d, 1d), (thread.Anchor.X, thread.Anchor.Y), "The spot stays on the element's box.");
    }

    [TestMethod]
    public void AReviewersReply_Should_ReopenAThreadAndRaiseItsSequence()
    {
        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor, "R", "first", false));
        Edit(() => WorkbenchReviewDocument.SetStatus(Threads, "a", WorkbenchReviewStatus.Done, fromAgent: true));

        var before = Thread("a").Sequence;

        Assert.IsTrue(WorkbenchReviewDocument.AddMessage(Threads, "a", fromAgent: false, "R", "Not quite"));

        var thread = Thread("a");

        Assert.AreEqual(WorkbenchReviewStatus.Open, thread.Status);
        Assert.IsGreaterThan(before, thread.Sequence);
        Assert.HasCount(2, thread.Messages);
    }

    [TestMethod]
    public void TheAgentsReply_Should_NeitherReopenNorWakeTheAgentItself()
    {
        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor, "R", "first", false));
        Edit(() => WorkbenchReviewDocument.SetStatus(Threads, "a", WorkbenchReviewStatus.Done, fromAgent: true));

        var before = Thread("a").Sequence;

        Edit(() => WorkbenchReviewDocument.AddMessage(Threads, "a", fromAgent: true, "Claude", "Done, it's bigger now"));

        var thread = Thread("a");

        Assert.AreEqual(WorkbenchReviewStatus.Done, thread.Status);
        Assert.AreEqual(before, thread.Sequence);
        Assert.IsTrue(thread.Messages[^1].FromAgent);
    }

    [TestMethod]
    public void AReviewerReopeningAThread_Should_RaiseItsSequence()
    {
        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor, "R", "first", false));

        var created = Thread("a").Sequence;

        Edit(() => WorkbenchReviewDocument.SetStatus(Threads, "a", WorkbenchReviewStatus.Working, fromAgent: true));
        Assert.AreEqual(created, Thread("a").Sequence, "The agent's own status changes don't wake it.");

        Edit(() => WorkbenchReviewDocument.SetStatus(Threads, "a", WorkbenchReviewStatus.Open, fromAgent: false));
        Assert.IsGreaterThan(created, Thread("a").Sequence);
    }

    [TestMethod]
    public void AnEditOfAThreadThatIsGone_Should_ReturnFalse()
    {
        Assert.IsFalse(WorkbenchReviewDocument.AddMessage(Threads, "missing", false, "R", "text"));
        Assert.IsFalse(WorkbenchReviewDocument.SetStatus(Threads, "missing", WorkbenchReviewStatus.Done, true));
        Assert.IsFalse(WorkbenchReviewDocument.SetTasks(Threads, "missing", []));
    }

    [TestMethod]
    public void Tasks_Should_BeReplacedAsAWholeAndCapped()
    {
        Edit(() => WorkbenchReviewDocument.AddThread(Threads, "a", "theme", "", anchor, "R", "first", false));

        Edit(() => WorkbenchReviewDocument.SetTasks(Threads, "a", Enumerable.Range(1, 30).Select(i => new WorkbenchReviewTask($" step {i} ", i <= 2))));

        var tasks = Thread("a").Tasks;

        Assert.HasCount(WorkbenchReviewDocument.MaxTasks, tasks);
        Assert.AreEqual(new WorkbenchReviewTask("step 1", true), tasks[0]);
        Assert.IsFalse(tasks[2].Done);

        Edit(() => WorkbenchReviewDocument.SetTasks(Threads, "a", [new("only", false)]));

        Assert.HasCount(1, Thread("a").Tasks);
    }

    [TestMethod]
    public void APageStatus_Should_RecordWhoSetItAndWhen()
    {
        var pages = document.GetMap("pages");

        Edit(() => WorkbenchReviewDocument.SetPageStatus(pages, "buildings", WorkbenchPageStatus.Approved, "Sarah"));

        var page = WorkbenchReviewDocument.ReadPages(pages)["buildings"];

        Assert.AreEqual(WorkbenchPageStatus.Approved, page.Status);
        Assert.AreEqual("Sarah", page.By);
        Assert.IsLessThan(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow - page.At!.Value);
    }

    [TestMethod]
    public void TheAgent_Should_CountAsOnlineForTwoMinutes()
    {
        var agent = document.GetMap("agent");

        Assert.IsNull(WorkbenchReviewDocument.ReadAgent(agent), "No agent has called yet.");

        Edit(() => WorkbenchReviewDocument.SetAgent(agent, "Claude Code", "a"));

        var info = WorkbenchReviewDocument.ReadAgent(agent)!;

        Assert.AreEqual(("Claude Code", "a"), (info.Name, info.ThreadId));
        Assert.IsTrue(info.IsActive);
        Assert.IsFalse((info with { At = DateTimeOffset.UtcNow.AddMinutes(-3) }).IsActive);
    }

    [TestMethod]
    public void NextSequence_Should_OnlyGrowEvenWhenCalledAtOnce()
    {
        var values = new ConcurrentBag<long>();

        Parallel.For(0, 2_000, _ => values.Add(WorkbenchReviewDocument.NextSequence()));

        Assert.HasCount(2_000, values.Distinct());
    }

    [TestMethod]
    public void AnUnreadableEntry_Should_ReadAsItsDefaults()
    {
        Edit(() =>
        {
            Threads.Set("not-a-map", "text");
            Threads.Set("bad", new BcrdtMap());
        });

        var threads = WorkbenchReviewDocument.ReadThreads(Threads);

        Assert.HasCount(1, threads);
        Assert.AreEqual(WorkbenchReviewStatus.Open, threads[0].Status);
        Assert.AreEqual("body", threads[0].Anchor.Selector);
        Assert.IsEmpty(threads[0].Messages);
    }
}
