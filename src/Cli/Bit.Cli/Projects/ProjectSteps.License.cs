using Bit.Cli.Infrastructure;
using Bit.Cli.Licensing;

namespace Bit.Cli.Projects;

public sealed partial class ProjectSteps
{
    /// <summary>
    /// Asks bit Fabric for the new project's trial, which names the project's id and its first commit, and commits it in
    /// bit.json. Without an answer the project goes on unlicensed, and the next bit command in it gives the link.
    /// </summary>
    public async Task<StepResult> TrialLicenseAsync(Action<string> progress, CancellationToken cancellationToken)
    {
        const string title = "No trial license yet";

        if (BitJsonFile.Read(Path.Combine(project.Directory, ProjectBit.FileName)) is not { ProjectId: { } id } bitJson)
            return StepResult.Skipped("Skipped the trial license", "the project has no bit.json");

        var roots = await Git(["rev-list", "--max-parents=0", "HEAD"], cancellationToken);

        if ((roots.Succeeded ? roots.OutputLines.Select(line => line.Trim()).LastOrDefault(ProjectLicense.IsCommit) : null) is not { } commit)
            return StepResult.Skipped(title, "the project has no git commit yet");

        var document = await new LicenseServer(cli.License, cli.Log).TrialAsync(id, bitJson.Name, commit, cancellationToken);

        if (document is null)
            return StepResult.Skipped(title, "bit Fabric didn't answer; run bit license later");

        if (ProjectLicense.Verify(document, cli.License.PublicKeys) is not { } license || license.Project != id || license.Commit != commit)
            return StepResult.Skipped(title, "bit Fabric's answer didn't check out");

        bitJson.SetLicense(document);

        var until = license.Expires.UtcDateTime.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

        // Committed only in the project's own repository, never in one it was created inside.
        var committed = await CommitPathAsync(bitJson.FilePath, "Add the project's trial license", cancellationToken);

        return StepResult.Succeeded("Got a trial license", $"until {until}, in bit.json{(committed is null ? "" : ", committed")}");
    }
}
