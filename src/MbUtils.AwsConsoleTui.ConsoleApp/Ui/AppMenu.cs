using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class AppMenu
{
    // onShowStacks wires the active CloudFormation > Stacks item; null keeps it inert.
    public static MenuBar Build(IApplication app, Action? onShowStacks = null)
    {
        // API change: MenuItem.CanExecute does not exist in v2.4.7; use Enabled property instead.
        // API change: MenuItem(title, help, action) 3-arg ctor doesn't exist; use (commandText, helpText, action, Key) and pass Key.Empty.
        MenuItem Placeholder(string title) => new(title, "Coming soon", () => { }, Key.Empty) { Enabled = false };

        var stacksItem = new MenuItem("_Stacks", "List CloudFormation stacks", () => onShowStacks?.Invoke(), Key.Empty)
        {
            // API change: CanExecute replaced by Enabled; mirror whether handler is wired
            Enabled = onShowStacks is not null,
        };

        return new MenuBar(
        [
            new MenuBarItem("_File",
            [
                new MenuItem("_Quit", "Exit the application", () => app.RequestStop(), Key.Empty),
            ]),
            new MenuBarItem("_CloudFormation",
            [
                stacksItem,
                Placeholder("Stack _Details"),
            ]),
            new MenuBarItem("_S3",
            [
                Placeholder("_Buckets"),
            ]),
            new MenuBarItem("_Lambda",
            [
                Placeholder("_Functions"),
            ]),
            new MenuBarItem("S_QS",
            [
                Placeholder("_Queues"),
            ]),
        ]);
    }
}
