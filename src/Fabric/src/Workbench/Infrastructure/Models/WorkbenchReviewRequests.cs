namespace Bit.Fabric.Workbench.Infrastructure.Models;

/// <summary>The body of an agent's reply to a comment over HTTP.</summary>
public sealed record WorkbenchReviewReplyRequest(string Text, string? Agent);

/// <summary>The body that sets a comment's or a page's status over HTTP.</summary>
public sealed record WorkbenchReviewStatusRequest(string Status, string? Agent);

/// <summary>The body that replaces a comment's task list over HTTP.</summary>
public sealed record WorkbenchReviewTasksRequest(WorkbenchReviewTask[] Tasks, string? Agent);
