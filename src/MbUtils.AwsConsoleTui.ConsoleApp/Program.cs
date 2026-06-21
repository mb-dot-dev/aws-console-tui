using MbUtils.AwsConsoleTui.ConsoleApp.Ui;
using MbUtils.AwsConsoleTui.Core.Aws;
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

    var menu = AppMenu.Build();
    var status = AppStatusBar.Build(context);

    // Placeholder content; Task 7 replaces this with the StacksView.
    // API change: Toplevel does not exist in v2.4.7; Window implements IRunnable and serves as top-level.
    var content = new Window
    {
        Title = "CloudFormation Stacks",
        X = 0,
        Y = Pos.Bottom(menu),
        Width = Dim.Fill(),
        Height = Dim.Fill(1),
    };

    // Use a Window as the top-level IRunnable (Toplevel was removed in v2)
    var top = new Window();
    top.Add(menu, content, status);
    Application.Run(top);
    top.Dispose();
}
finally
{
    Application.Shutdown();
}
