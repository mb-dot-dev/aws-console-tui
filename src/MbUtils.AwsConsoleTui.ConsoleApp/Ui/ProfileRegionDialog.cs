using System.Collections.ObjectModel;
using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class ProfileRegionDialog
{
    public static (string? Profile, string? Region) Show(IProfileProvider provider)
    {
        var profiles = provider.GetProfileNames();
        var regions = provider.GetRegions();

        if (profiles.Count == 0)
        {
            // API change: MessageBox.ErrorQuery requires IApplication as first param in v2.4.7
            MessageBox.ErrorQuery(Application.Instance, "No AWS profiles", "No named profiles found in your AWS config.", "OK");
            return (null, null);
        }

        var dialog = new Dialog
        {
            Title = "Select AWS Profile and Region",
            Width = 60,
            Height = 18,
        };

        var profileLabel = new Label { Text = "Profile:", X = 1, Y = 0 };
        var profileList = new ListView
        {
            X = 1, Y = 1, Width = 26, Height = 12,
            // API change: ListWrapper<T> ctor takes ObservableCollection<T>, not List<T>
            Source = new ListWrapper<string>(new ObservableCollection<string>(profiles)),
        };

        var regionLabel = new Label { Text = "Region:", X = 30, Y = 0 };
        var regionList = new ListView
        {
            X = 30, Y = 1, Width = 26, Height = 12,
            // API change: ListWrapper<T> ctor takes ObservableCollection<T>, not List<T>
            Source = new ListWrapper<string>(new ObservableCollection<string>(regions)),
        };

        SelectDefault(profileList, profiles, provider.DefaultProfile);
        SelectDefault(regionList, regions, provider.DefaultRegion);

        (string? Profile, string? Region) result = (null, null);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, _) =>
        {
            // API change: SelectedItem is int? (nullable) in v2.4.7
            result = (
                profiles[profileList.SelectedItem.GetValueOrDefault(0)],
                regions[regionList.SelectedItem.GetValueOrDefault(0)]
            );
            Application.RequestStop();
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, _) => Application.RequestStop();

        dialog.Add(profileLabel, profileList, regionLabel, regionList);
        // API change: Dialog.AddButton() does not exist in v2.4.7; use Buttons property instead
        dialog.Buttons = [ok, cancel];

        Application.Run(dialog);
        dialog.Dispose();
        return result;
    }

    private static void SelectDefault(ListView list, IReadOnlyList<string> items, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var index = items.ToList().FindIndex(i => string.Equals(i, value, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            list.SelectedItem = index;
        }
    }
}
