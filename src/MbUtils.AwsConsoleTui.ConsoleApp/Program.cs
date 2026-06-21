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
var (profile, region) = ProfileRegionDialog.Show(app, profileProvider);
if (profile is null || region is null)
{
    return;
}

var context = new AwsContext();
context.Set(profile, region);
var clientFactory = new AwsClientFactory(context);

ICloudFormationService ServiceFactory() =>
    new CloudFormationService(new CloudFormationClient(clientFactory.CreateCloudFormationClient()));

var stacksView = new StacksView(app, ServiceFactory)
{
    X = 0,
    Y = 0,
    Width = Dim.Fill(),
};

var menu = AppMenu.Build(app, onShowStacks: stacksView.Reload);
var status = AppStatusBar.Build(app, context);

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
top.Add(menu, content, status);

// Initialized fires once on first run — load the stacks on startup.
top.Initialized += (_, _) => stacksView.Reload();

app.Run(top);
top.Dispose();
