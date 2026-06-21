using MbUtils.AwsConsoleTui.Core.CloudFormation;
using MbUtils.AwsConsoleTui.Core.Models;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MbUtils.AwsConsoleTui.ConsoleApp.Ui;

public sealed class StacksView : View
{
    private readonly Func<ICloudFormationService> _serviceFactory;
    private readonly TextField _filter;
    private readonly TableView _table;
    private readonly SpinnerView _spinner;
    private readonly Label _statusLabel;

    private IReadOnlyList<StackInfo> _allStacks = Array.Empty<StackInfo>();
    private bool _isLoading;

    public StacksView(Func<ICloudFormationService> serviceFactory)
    {
        _serviceFactory = serviceFactory;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var filterLabel = new Label { Text = "Filter:", X = 0, Y = 0 };
        _filter = new TextField { X = Pos.Right(filterLabel) + 1, Y = 0, Width = 30 };
        // API note: TextChanged is EventHandler (no generic arg) in v2.4.7 — (_, _) discards both sender and EventArgs.
        _filter.TextChanged += (_, _) => ApplyFilter();

        _spinner = new SpinnerView
        {
            X = Pos.Right(_filter) + 2,
            Y = 0,
            Visible = false,
            // AutoSpin is bool; setting true starts the internal timer-driven animation.
            AutoSpin = false,
        };

        _statusLabel = new Label { Text = "", X = Pos.Right(_spinner) + 2, Y = 0, Width = Dim.Fill() };

        _table = new TableView
        {
            X = 0,
            Y = Pos.Bottom(_filter) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
        };

        // Row selection seam for a future Stack Details view (intentionally inert in v1).
        // API change: CellActivated does not exist in v2.4.7.
        // The View.Accepted event fires when Command.Accept is invoked (Enter on a focused view).
        _table.Accepted += (_, _) => { };

        Add(filterLabel, _filter, _spinner, _statusLabel, _table);

        // API note: KeyDown is EventHandler<Key> in v2.4.7 — second arg is the Key itself,
        // which carries a mutable Handled property to stop further processing.
        KeyDown += (_, key) =>
        {
            if (key == Key.F5)
            {
                Reload();
                key.Handled = true;
            }
        };
    }

    public void Reload()
    {
        if (_isLoading)
        {
            return;
        }

        SetLoading(true);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var service = _serviceFactory();
            var stacks = await service.ListStacksAsync(CancellationToken.None);
            // Marshal back to the UI thread via Application.Invoke(Action).
            Application.Invoke(() =>
            {
                _allStacks = stacks;
                ApplyFilter();
                SetLoading(false);
                _statusLabel.Text = $"{_allStacks.Count} stack(s)";
            });
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                SetLoading(false);
                _statusLabel.Text = "Error";
                // API change: MessageBox.ErrorQuery requires IApplication as first arg in v2.4.7.
                MessageBox.ErrorQuery(Application.Instance, "Failed to load stacks", ex.Message, "OK");
            });
        }
    }

    private void ApplyFilter()
    {
        var filtered = StackFilter.Apply(_allStacks, _filter.Text);
        _table.Table = new EnumerableTableSource<StackInfo>(
            filtered,
            new Dictionary<string, Func<StackInfo, object>>
            {
                ["Name"] = s => s.Name,
                ["Status"] = s => s.Status,
                ["Created"] = s => s.CreatedAt.ToString("u"),
                ["Last Updated"] = s => s.LastUpdatedAt?.ToString("u") ?? "-",
                ["Description"] = s => s.Description ?? "-",
            });
        // Setting Table calls Update() internally; SetNeedsDraw() queues a redraw pass.
        _table.SetNeedsDraw();
    }

    private void SetLoading(bool loading)
    {
        _isLoading = loading;
        _spinner.Visible = loading;
        _spinner.AutoSpin = loading;
        if (loading)
        {
            _statusLabel.Text = "Loading…";
        }
    }
}
