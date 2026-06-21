using System.Collections.ObjectModel;
using MbUtils.AwsConsoleTui.Core.Aws;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public static class ProfileRegionDialog
{
    public static (string? Profile, string? Region) Show(IApplication app, IProfileProvider provider)
    {
        var profiles = provider.GetProfileNames();
        var regions = provider.GetRegions();

        if (profiles.Count == 0)
        {
            MessageBox.ErrorQuery(app, "No AWS profiles", "No named profiles found in your AWS config.", "OK");
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

        // Enter on the profile list advances to the region list instead of
        // confirming; marking handled stops the Accept reaching the OK button.
        profileList.Accepting += (_, e) =>
        {
            e.Handled = true;
            regionList.SetFocus();
        };

        var help = new Label
        {
            Text = "Up/Down: choose   Tab: next field   Enter: next / confirm   Esc: cancel",
            X = 1,
            Y = 13,
        };

        (string? Profile, string? Region) result = (null, null);

        var ok = new Button { Text = "OK", IsDefault = true };
        ok.Accepting += (_, e) =>
        {
            // SelectedItem is int? in v2.4.7; both lists always have a selection
            // (pre-seeded with defaults / index 0), so GetValueOrDefault(0) is safe.
            result = (
                profiles[profileList.SelectedItem.GetValueOrDefault(0)],
                regions[regionList.SelectedItem.GetValueOrDefault(0)]
            );
            // Mark handled so the Accept command does not propagate past this
            // dialog and stop the main window too (which exits the whole app).
            e.Handled = true;
            app.RequestStop();
        };

        var cancel = new Button { Text = "Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop();
        };

        // Esc cancels the dialog (leaves result null) without confirming.
        dialog.KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                key.Handled = true;
                app.RequestStop();
            }
        };

        dialog.Add(profileLabel, profileList, regionLabel, regionList, help);
        // API change: Dialog.AddButton() does not exist in v2.4.7; use Buttons property instead
        dialog.Buttons = [ok, cancel];

        app.Run(dialog);
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
