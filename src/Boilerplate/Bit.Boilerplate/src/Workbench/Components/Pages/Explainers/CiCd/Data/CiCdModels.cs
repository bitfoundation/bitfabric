namespace Boilerplate.Workbench.Components.Pages.Explainers.CiCd.Data;

/// <summary>A point of the summary at the top: a short title and a sentence.</summary>
public sealed record CiCdHighlight(string IconName, string Title, string Text);

/// <summary>A column of the pipeline's diagram: where its boxes run, and the boxes, top to bottom.</summary>
public sealed record CiCdStage(string Label, string IconName, CiCdNode[] Nodes, bool IsYourServer = false);

/// <summary>A box of the diagram: a workflow, a job, or what the pipeline ends in. <paramref name="IsOff"/> marks a job that waits to be turned on.</summary>
public sealed record CiCdNode(string Title, string Text, bool IsCode = false, bool IsOff = false);

/// <summary>A workflow file: what starts it, what it does and, where it helps, the command that starts it.</summary>
public sealed record CiCdWorkflow(string File, string Starts, string Does, string IconName, string? Command = null);

/// <summary>An environment the CD deploys to, as label and value rows.</summary>
public sealed record CiCdEnvironment(string Name, string IconName, CiCdFact[] Facts);

public sealed record CiCdFact(string Label, string Value, bool IsCode = false);

/// <summary>A job of cd-template.yml: the machine it runs on and what it does. <paramref name="IsOff"/> marks one that is off until the app ships on its platform.</summary>
public sealed record CiCdJob(string Name, string Runner, bool OnYourServer, string Does, bool IsOff = false);

/// <summary>A step of a server deployment, in the order the deploy job runs them.</summary>
public sealed record CiCdStep(string Title, string Text, string IconName);

public enum CiCdSettingKind
{
    Variable,
    Secret
}

/// <summary>A variable or a secret of a GitHub environment, which each of Test and Production holds a value of its own for.</summary>
public sealed record CiCdSetting(string Name, CiCdSettingKind Kind, string Holds, bool IsOptional = false);
