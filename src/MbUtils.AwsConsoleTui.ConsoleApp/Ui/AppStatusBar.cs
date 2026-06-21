using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class AppStatusBar
{
    public static StatusBar Build(IApplication app, IAwsContext context)
    {
        // API note: Shortcut ctor is (Key key, String commandText, Action action, String helpText) in v2.4.7.
        return new StatusBar(
        [
            new Shortcut(Key.Empty, $"Profile: {context.ProfileName}", null),
            new Shortcut(Key.Empty, $"Region: {context.Region}", null),
            new Shortcut(Key.F5, "Refresh", null),
            new Shortcut(Key.Q.WithCtrl, "Quit", () => app.RequestStop()),
        ]);
    }
}
