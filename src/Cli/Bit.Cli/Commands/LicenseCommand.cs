using System.CommandLine;
using Bit.Cli.Licensing;
using Spectre.Console;

namespace Bit.Cli.Commands;

public static class LicenseCommand
{
    public static Command Create(Func<CliServices> services)
    {
        var licenseArgument = new Argument<string?>("license") { Description = "A license from bitfabric.co/license to save in bit.json, after bit checks that it is this project's. Without it, bit shows the project's license and the link that gets it one, free.", Arity = ArgumentArity.ZeroOrOne };
        var projects = new ProjectOptions();

        var command = new Command("license", "Show the project's license, or save one in its bit.json.")
        {
            licenseArgument
        };

        projects.AddTo(command, projects.Project);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var check = new LicenseCheck(cli);

            if (BitJsonFile.Find(projects.Folder(parseResult, cli)) is not { } bitJson)
                return cli.UsageError("not-a-project", "There's no bit.json here or in a folder above, so this isn't a project bit made.");

            bitJson.EnsureProjectId();

            if (parseResult.GetValue(licenseArgument)?.Trim() is { Length: > 0 } given)
            {
                var givenStatus = await check.EvaluateAsync(bitJson, given, cancellationToken, given: true);

                if (givenStatus.Stops)
                {
                    await check.SayAsync(bitJson, givenStatus, cancellationToken);
                    cli.Console.Fail("bit.json is left as it was.");
                    return CliApp.ExitFailed;
                }

                bitJson.SetLicense(given);
                cli.Console.Out.MarkupLine($"[green]{cli.Console.OkSymbol}[/] {Markup.Escape($"Saved the license in {bitJson.FilePath}. {check.Describe(givenStatus)}")}");
                await check.SayAsync(bitJson, givenStatus, cancellationToken);

                return CliApp.ExitOk;
            }

            var status = await check.EvaluateAsync(bitJson, bitJson.License, cancellationToken);

            cli.Console.Out.MarkupLine($"[bold]{Markup.Escape(bitJson.Name)}[/] [grey]{bitJson.ProjectId:D}[/]");
            cli.Console.Out.WriteLine(check.Describe(status));
            await check.SayAsync(bitJson, status, cancellationToken);

            if (status.Lines.Any(line => line.Link) is false)
            {
                cli.Console.Out.Profile.Out.Writer.WriteLine($"Renew it, free, any time: {await check.LinkAsync(bitJson, status, cancellationToken)}");
            }

            return status.Stops is false || await check.WaitAsync(bitJson, cancellationToken) ? CliApp.ExitOk : CliApp.ExitFailed;
        });

        return command;
    }
}
