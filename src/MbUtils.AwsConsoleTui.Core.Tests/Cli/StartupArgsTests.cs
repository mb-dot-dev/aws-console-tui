using MbUtils.AwsConsoleTui.Core.Cli;
using Xunit;

namespace MbUtils.AwsConsoleTui.Core.Tests.Cli;

public class StartupArgsTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void VersionFlag_ReturnsShowVersion(string arg)
        => Assert.Equal(StartupAction.ShowVersion, StartupArgs.Parse(new[] { arg }));

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpFlag_ReturnsShowHelp(string arg)
        => Assert.Equal(StartupAction.ShowHelp, StartupArgs.Parse(new[] { arg }));

    [Fact]
    public void NoArgs_ReturnsRunTui()
        => Assert.Equal(StartupAction.RunTui, StartupArgs.Parse(Array.Empty<string>()));

    [Fact]
    public void UnknownArg_ReturnsRunTui()
        => Assert.Equal(StartupAction.RunTui, StartupArgs.Parse(new[] { "--frobnicate" }));

    [Fact]
    public void FirstRecognizedFlagInOrderWins()
        => Assert.Equal(StartupAction.ShowHelp, StartupArgs.Parse(new[] { "--help", "--version" }));
}
