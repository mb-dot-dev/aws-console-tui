namespace MbUtils.AwsConsoleTui.Core.Cli;

public enum StartupAction
{
    RunTui,
    ShowVersion,
    ShowHelp,
}

public static class StartupArgs
{
    public static StartupAction Parse(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--version":
                case "-v":
                    return StartupAction.ShowVersion;
                case "--help":
                case "-h":
                    return StartupAction.ShowHelp;
            }
        }

        return StartupAction.RunTui;
    }
}
