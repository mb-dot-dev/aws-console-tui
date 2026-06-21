using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// v2 instance-based application lifecycle: Create + Init returns an IApplication
// that owns its resources and is disposed by the using block (replaces the
// legacy static Application.Init/Run/Shutdown).
using IApplication app = Application.Create().Init();

var profileProvider = new ProfileProvider();
var context = new AwsContext();
using var clientFactory = new AwsClientFactory(context);

ICloudFormationService ServiceFactory() =>
    new CloudFormationService(new CloudFormationClient(clientFactory.GetCloudFormationClient()));

var stacksView = new StacksView(app, ServiceFactory)
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
};

var menu = AppMenu.Build(app, onShowStacks: stacksView.Reload);

var content = new Window
{
    Title = "CloudFormation Stacks",
    X = 0,
    Y = Pos.Bottom(menu),
    Width = Dim.Fill(),
    Height = Dim.Fill(1),
};
content.Add(stacksView);

// Window serves as the top-level IRunnable (Toplevel was removed in v2).
var top = new Window();
top.Add(menu, content);

// Run the main window once. Once its loop is live, show the profile/region
// picker as a modal on top of it (the v2 session-stack pattern). Deferring with
// app.Invoke ensures the main loop is iterating before the nested modal runs.
top.Initialized += (_, _) => app.Invoke(() =>
{
    var (profile, region) = ProfileRegionDialog.Show(app, profileProvider);
    if (profile is null || region is null)
    {
        app.RequestStop(top);
        return;
    }

    context.Set(profile, region);
    // Status bar is built now that the profile/region are known, then docked
    // into the running window.
    top.Add(AppStatusBar.Build(app, context));
    top.SetNeedsDraw();
    stacksView.Reload();
});

app.Run(top);
top.Dispose();
