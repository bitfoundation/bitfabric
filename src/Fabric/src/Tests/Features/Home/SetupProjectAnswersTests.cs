using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;
using Bit.Fabric.Client.Core.Components.Pages.SetupProject;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// The line the page that sets up a project that already exists gives (bitfabric-internal/specs/getting-started.md, Setting
/// up an existing project): a plain unit test, since the line is a pure function of the answers. The repository's address is
/// the one word of it the visitor typed, and a shared link carries it, so an address a shell would read anything in never
/// gets a line.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public partial class SetupProjectAnswersTests
{
    [TestMethod]
    public void ARepositoryOnWindows_Should_BeClonedBySetupInsidePowerShellsQuotes()
    {
        var answers = new SetupProjectAnswers { Repository = " https://github.com/acme/Bluebird/ ", Computer = NewProjectComputer.Windows, Devices = [NewProjectDevice.Android] };

        Assert.AreEqual("powershell -c \"irm https://bitfabric.co/install.ps1 | iex; bit setup https://github.com/acme/Bluebird --platforms android --yes\"", answers.InstallLine);
        Assert.AreEqual("Bluebird", answers.RepositoryName);
    }

    [TestMethod]
    [DataRow(NewProjectComputer.MacOs)]
    [DataRow(NewProjectComputer.Linux)]
    public void ARepositoryOnMacOrLinux_Should_BeHandedToTheInstallersScript(NewProjectComputer computer)
    {
        var answers = new SetupProjectAnswers { Repository = "git@github.com:acme/Bluebird.git", Computer = computer };

        Assert.AreEqual("curl -fsSL https://bitfabric.co/install.sh | bash -s -- setup git@github.com:acme/Bluebird.git --yes", answers.InstallLine);
        Assert.AreEqual("Bluebird", answers.RepositoryName);
    }

    [TestMethod]
    public void AProjectOnThisComputer_Should_BeSetUpWhereTheLineIsPasted()
    {
        var answers = new SetupProjectAnswers { Repository = "https://github.com/acme/Bluebird", IsOnThisComputer = true, Computer = NewProjectComputer.Linux, Devices = [NewProjectDevice.Android, NewProjectDevice.Ios] };

        Assert.IsTrue(answers.HasLine);
        Assert.AreEqual("curl -fsSL https://bitfabric.co/install.sh | bash -s -- setup --platforms android,ios --yes", answers.InstallLine);
        Assert.AreEqual("https://bitfabric.co/setup?here=1&devices=android,ios", answers.ShareLink);
    }

    [TestMethod]
    [DataRow("https://github.com/acme/Bluebird")]
    [DataRow("https://github.com/acme/Bluebird.git")]
    [DataRow("https://gitlab.com/acme/apps/Bluebird.Web")]
    [DataRow("https://dev.azure.com/acme/Shop/_git/Bluebird")]
    [DataRow("https://git.acme.com:8443/team/blue_bird~2")]
    [DataRow("git@github.com:acme/Bluebird.git")]
    public void ARepositorysAddress_Should_GetALine(string address)
    {
        var answers = new SetupProjectAnswers { Repository = address, Computer = NewProjectComputer.Windows };

        Assert.IsTrue(answers.HasLine, address);
        Assert.Contains($"bit setup {address} --yes", answers.InstallLine);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Bluebird")]
    [DataRow("https://github.com/acme")]
    [DataRow("https://github.com/acme/Bluebird?tab=readme")]
    [DataRow("https://github.com/acme/Bluebird#readme")]
    [DataRow("https://github.com/acme/Bluebird; rm -rf ~")]
    [DataRow("https://github.com/acme/Bluebird\"; calc; \"")]
    [DataRow("https://github.com/acme/$(whoami)")]
    [DataRow("https://github.com/acme/`whoami`")]
    [DataRow("https://github.com/acme/%USERPROFILE%")]
    [DataRow("https://github.com/acme/Bluebird&calc")]
    [DataRow("https://github.com/acme/Blue bird")]
    [DataRow("https://github.com/acme/..")]
    [DataRow("https://someone@github.com/acme/Bluebird")]
    [DataRow("http://github.com/acme/Bluebird")]
    [DataRow("ssh://git@github.com/acme/Bluebird")]
    public void AnAddressAShellCouldReadAnythingIn_Should_GetNoLine(string address)
    {
        var answers = new SetupProjectAnswers { Repository = address, Computer = NewProjectComputer.Windows };

        Assert.IsFalse(answers.HasLine, address);
        Assert.IsNull(answers.RepositoryName, address);
    }

    [TestMethod]
    public void TheShareLink_Should_CarryTheAddressAndTheApps()
    {
        var answers = new SetupProjectAnswers { Repository = "https://github.com/acme/Bluebird", Computer = NewProjectComputer.MacOs, Devices = [NewProjectDevice.Ios] };

        Assert.AreEqual("https://bitfabric.co/setup?repo=https%3A%2F%2Fgithub.com%2Facme%2FBluebird&devices=ios", answers.ShareLink);
    }
}
