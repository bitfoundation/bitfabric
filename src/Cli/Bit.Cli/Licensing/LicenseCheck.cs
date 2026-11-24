using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Bit.Cli.Infrastructure;
using Spectre.Console;

namespace Bit.Cli.Licensing;

/// <summary>A line the check says; <see cref="Link"/> puts the link that gets the project its license after it.</summary>
public sealed record LicenseLine(string Text, bool Link = false);

/// <summary>What the check found: the license, when it is one for this project, whether commands stop, and what to say.</summary>
public sealed record LicenseStatus(ProjectLicense? License, bool Stops, IReadOnlyList<LicenseLine> Lines);

/// <summary>
/// The project's license, checked before a command with nothing but the project's own files: the signature, the project's
/// id in bit.json, and that the commit the license names is the first of this repository's history. A trial is silent for
/// two weeks, then says when it ends, and once it has ended the commands stop with the link that gets the project its free
/// license. A free license says when it is about to end, and keeps working for two weeks after.
/// </summary>
public sealed class LicenseCheck(CliServices cli)
{
    /// <summary>The commands that don't work on a project, and <c>bit license</c> itself.</summary>
    public static readonly IReadOnlySet<string> CommandsWithoutACheck = new HashSet<string>(Infrastructure.SelfUpdate.CommandsWithoutAProject) { "license" };

    public static readonly TimeSpan QuietTrial = TimeSpan.FromDays(14);

    public static readonly TimeSpan RenewalNotice = TimeSpan.FromDays(30);

    public static readonly TimeSpan Grace = TimeSpan.FromDays(14);

    /// <summary>How far ahead of this computer's clock a license may be dated, for a clock that is a little behind.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromDays(1);

    private readonly string code = Base64UrlCode();

    private LicenseOptions Options => cli.License;

    private DateTimeOffset Now => Options.Clock.GetUtcNow();

    /// <summary>Whether bit can wait for the license here: someone is at the terminal, and it isn't CI.</summary>
    public bool CanWait => cli.IsInteractive && cli.Environment.IsCI is false;

    /// <summary>The first bit Boilerplate whose projects <c>bit new</c> gives an id and a license.</summary>
    public const string FirstLicensedTemplate = "11.0.0-pre-24";

    /// <summary>
    /// Whether the project <paramref name="bitJson"/> belongs to has a license to check: every project on
    /// <see cref="FirstLicensedTemplate"/> or later, with an id or without, and one whose bit.json doesn't say its version.
    /// A project on an older bit Boilerplate never had an id, and bit Boilerplate's own source folder isn't a project.
    /// </summary>
    public static bool IsLicensed(BitJsonFile bitJson) => bitJson.IsTemplateSource is false
        && (bitJson.TemplateVersion is not { } version
            || Infrastructure.SelfUpdate.IsValidVersion(version) is false
            || Infrastructure.SelfUpdate.CompareVersions(version, FirstLicensedTemplate) >= 0);

    /// <summary>Before a command that works on the project in <paramref name="folder"/>: true when the command goes on.</summary>
    public async Task<bool> BeforeCommandAsync(string folder, CancellationToken cancellationToken)
    {
        if (Options.ChecksCommands is false || BitJsonFile.Find(folder) is not { } bitJson || IsLicensed(bitJson) is false)
            return true;

        if (bitJson.ProjectId is null)
        {
            // Without an id there is no link to give; bit license makes one.
            await SayAsync(bitJson, new LicenseStatus(null, true, [new("This project has no id in bit.json, so it has no license yet. Run bit license to give it its id and the link that gets it a license, free.")]), cancellationToken);
            return false;
        }

        var status = await EvaluateAsync(bitJson, bitJson.License, cancellationToken);

        await SayAsync(bitJson, status, cancellationToken);

        return status.Stops is false || await WaitAsync(bitJson, cancellationToken);
    }

    /// <summary>
    /// What <paramref name="document"/> is worth as the license of the project <paramref name="bitJson"/> belongs to;
    /// <paramref name="given"/> for one that isn't in bit.json yet.
    /// </summary>
    public async Task<LicenseStatus> EvaluateAsync(BitJsonFile bitJson, string? document, CancellationToken cancellationToken, bool given = false)
    {
        var theLicense = given ? "This license" : "The license in bit.json";

        if (document is null)
            return Stop("This project has no license yet. Get one, free:");

        var license = ProjectLicense.Verify(document, Options.PublicKeys);

        if (license is null)
            return Stop($"{theLicense} doesn't check out: it was changed, or bit Fabric didn't issue it. Get this project a new one, free:");

        if (license.Project != bitJson.ProjectId)
            return Stop($"{theLicense} belongs to another project. Get this project its own, free:");

        if (license.Issued > Now + ClockSkew)
            return Stop($"{theLicense} is dated in the future. Check this computer's date, or get a new license, free:");

        List<LicenseLine> lines = [];

        switch (await FindCommitAsync(bitJson.Directory, license.Commit, cancellationToken))
        {
            case CommitPlace.InHistory:
                break;
            case CommitPlace.ShallowClone:
                lines.Add(new("This is a shallow clone, without the commit the project's license names. Run git fetch --unshallow to check it."));
                break;
            default:
                return Stop($"{theLicense} belongs to another project. Get this project its own, free:");
        }

        var expires = Day(license.Expires);

        if (license.IsTrial)
        {
            if (Now >= license.Expires)
                return new(license, true, [.. lines, new("The project's trial has ended. Get its free license:", Link: true)]);

            if (Now - license.Issued >= QuietTrial)
            {
                lines.Add(new($"The project's trial ends {In(license.Expires)}, on {expires}. Get its free license:", Link: true));
            }
        }
        else if (Now >= license.Expires + Grace)
        {
            return new(license, true, [.. lines, new($"The project's license ended on {expires}. Renew it, free:", Link: true)]);
        }
        else if (Now >= license.Expires)
        {
            lines.Add(new($"The project's license ended on {expires}, and bit keeps working until {Day(license.Expires + Grace)}. Renew it, free:", Link: true));
        }
        else if (Now >= license.Expires - RenewalNotice)
        {
            lines.Add(new($"The project's license ends {In(license.Expires)}, on {expires}. Renew it, free:", Link: true));
        }

        return new(license, false, lines);

        LicenseStatus Stop(string text) => new(null, true, [new(text, Link: true)]);
    }

    /// <summary>One plain sentence on the license <paramref name="status"/> found, for <c>bit license</c>.</summary>
    public string Describe(LicenseStatus status) => status.License switch
    {
        null => "This project has no license that checks out.",
        { IsTrial: true } trial => Now >= trial.Expires ? $"A trial, which ended on {Day(trial.Expires)}." : $"A trial, until {Day(trial.Expires)}.",
        var free => $"A free license{(free.Holder.Length > 0 ? $" for {free.Holder}" : "")}, {(Now >= free.Expires ? "which ended on" : "until")} {Day(free.Expires)}."
    };

    /// <summary>Says the lines of <paramref name="status"/>, the link after those that ask for it.</summary>
    public async Task SayAsync(BitJsonFile bitJson, LicenseStatus status, CancellationToken cancellationToken)
    {
        string? link = null;

        foreach (var line in status.Lines)
        {
            link ??= line.Link ? await LinkAsync(bitJson, status, cancellationToken) : null;

            cli.Console.Error.Markup($"[yellow]{cli.Console.WarnSymbol}[/] ");
            cli.Console.Error.Profile.Out.Writer.WriteLine(line.Link ? $"{line.Text} {link}" : line.Text);
        }
    }

    /// <summary>
    /// The page that gets the project its license, free: <c>https://bitfabric.co/license?project=&amp;id=&amp;commit=&amp;code=</c>,
    /// with the one-time code this bit waits for the license with.
    /// </summary>
    public async Task<string> LinkAsync(BitJsonFile bitJson, LicenseStatus status, CancellationToken cancellationToken)
    {
        var commit = await FirstCommitAsync(bitJson.Directory, cancellationToken) ?? (status.License is { } license && license.Project == bitJson.ProjectId ? license.Commit : null);

        var query = new List<string> { $"project={Uri.EscapeDataString(bitJson.Name)}", $"id={bitJson.ProjectId:D}" };

        if (commit is not null)
        {
            query.Add($"commit={commit}");
        }

        query.Add($"code={code}");

        return $"{LicenseOptions.SiteAddress}license?{string.Join('&', query)}";
    }

    /// <summary>
    /// Asks bit Fabric for the license issued with this bit's code, every few seconds for up to ten minutes, and saves it
    /// in bit.json once it checks out. Only with someone at the terminal; elsewhere the link is all bit says.
    /// </summary>
    public async Task<bool> WaitAsync(BitJsonFile bitJson, CancellationToken cancellationToken)
    {
        if (CanWait is false)
            return false;

        cli.Console.Error.MarkupLine("[grey]bit waits here for the license, and goes on once it's issued. Ctrl+C stops it.[/]");

        var server = new LicenseServer(Options, cli.Log);
        var waited = Stopwatch.StartNew();

        while (waited.Elapsed < Options.WaitLimit)
        {
            await Task.Delay(Options.PollInterval, cancellationToken);

            if (await server.ClaimAsync(code, cancellationToken) is not { } document)
                continue;

            var status = await EvaluateAsync(bitJson, document, cancellationToken, given: true);

            if (status.Stops)
            {
                await SayAsync(bitJson, status, cancellationToken);
                return false;
            }

            bitJson.SetLicense(document);
            cli.Console.Error.MarkupLine($"[green]{cli.Console.OkSymbol}[/] {Markup.Escape($"Got the project's license and saved it in bit.json. {Describe(status)}")}");

            return true;
        }

        cli.Console.Error.MarkupLine(Markup.Escape($"No license came in {(int)Options.WaitLimit.TotalMinutes} minutes. Run the command again once it's issued, or save it with: bit license <license>"));

        return false;
    }

    private enum CommitPlace
    {
        InHistory,
        ShallowClone,
        Elsewhere
    }

    /// <summary>
    /// Whether <paramref name="commit"/> is in the history of the repository's HEAD. Replace refs and the old grafts file
    /// are ignored, so <c>git replace --graft</c> can't give another repository the project's history.
    /// </summary>
    private async Task<CommitPlace> FindCommitAsync(string directory, string commit, CancellationToken cancellationToken)
    {
        var shallow = await GitAsync(directory, ["rev-parse", "--is-shallow-repository"], cancellationToken);

        if (shallow.Succeeded is false)
            return CommitPlace.Elsewhere;

        if ((await GitAsync(directory, ["cat-file", "-e", $"{commit}^{{commit}}"], cancellationToken)).Succeeded)
            return (await GitAsync(directory, ["merge-base", "--is-ancestor", commit, "HEAD"], cancellationToken)).Succeeded ? CommitPlace.InHistory : CommitPlace.Elsewhere;

        return shallow.OutputLines.Any(line => line.Trim() is "true") ? CommitPlace.ShallowClone : CommitPlace.Elsewhere;
    }

    /// <summary>The first commit of the repository's history; null without git, without a commit, or in a shallow clone.</summary>
    private async Task<string?> FirstCommitAsync(string directory, CancellationToken cancellationToken)
    {
        if ((await GitAsync(directory, ["rev-parse", "--is-shallow-repository"], cancellationToken)).OutputLines.Any(line => line.Trim() is "false") is false)
            return null;

        var roots = await GitAsync(directory, ["rev-list", "--max-parents=0", "HEAD"], cancellationToken);

        return roots.Succeeded ? roots.OutputLines.Select(line => line.Trim()).LastOrDefault(ProjectLicense.IsCommit) : null;
    }

    private async Task<ProcessResult> GitAsync(string directory, string[] arguments, CancellationToken cancellationToken)
    {
        if (cli.Runner.FindExecutable("git") is null)
            return new ProcessResult { ExitCode = -1, NotFound = true };

        return await cli.Runner.RunAsync(new ProcessSpec
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = directory,
            Environment = new Dictionary<string, string?>
            {
                ["GIT_NO_REPLACE_OBJECTS"] = "1",
                // A grafts file at a path that never exists, rather than the repository's .git/info/grafts.
                ["GIT_GRAFT_FILE"] = Path.Combine(Path.GetTempPath(), $"bit-no-grafts-{Guid.NewGuid():N}")
            },
            Timeout = TimeSpan.FromSeconds(30)
        }, cancellationToken);
    }

    private string In(DateTimeOffset end) => Math.Max(1, (int)Math.Ceiling((end - Now).TotalDays)) is var days && days == 1 ? "in a day" : $"in {days} days";

    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A one-time code: random and URL-safe.</summary>
    private static string Base64UrlCode() => System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(18));
}
