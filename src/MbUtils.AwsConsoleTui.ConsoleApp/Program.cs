using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
using MbUtils.AwsConsoleTui.Core.CloudFormation;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

Application.Init();
try
{
    var profileProvider = new ProfileProvider();
    var (profile, region) = ProfileRegionDialog.Show(profileProvider);
    if (profile is null || region is null)
    {
        return;
    }

    var context = new AwsContext();
    context.Set(profile, region);
    var clientFactory = new AwsClientFactory(context);

    ICloudFormationService ServiceFactory() =>
        new CloudFormationService(new CloudFormationClient(clientFactory.CreateCloudFormationClient()));

    var stacksView = new StacksView(ServiceFactory)
    {
        X = 0,
        Width = Dim.Fill(),
    };

    var menu = AppMenu.Build(onShowStacks: stacksView.Reload);
    var status = AppStatusBar.Build(context);

    var content = new Window
    {
        Title = "CloudFormation Stacks",
        X = 0,
        Y = Pos.Bottom(menu),
        Width = Dim.Fill(),
        Height = Dim.Fill(1),
    };
    content.Add(stacksView);

    // API change: Toplevel does not exist in v2.4.7; Window implements IRunnable and serves as top-level.
    var top = new Window();
    top.Add(menu, content, status);

    // API change: Loaded event does not exist in v2.4.7; use Initialized which fires once on first run.
    top.Initialized += (_, _) => stacksView.Reload();

    Application.Run(top);
    top.Dispose();
}
finally
{
    Application.Shutdown();
}
