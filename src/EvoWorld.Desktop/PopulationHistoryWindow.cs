using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace EvoWorld.Desktop;

// Scalar-only chart. No Core/world references, timers, or per-point drawing resources.
internal sealed class PopulationHistoryChart : IDisposable
{
    private readonly SKPaint _axis = new() { Color = new SKColor(104, 125, 148), StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _herbivores = new() { Color = new SKColor(65, 201, 235), StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _predators = new() { Color = new SKColor(213, 104, 255), StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPathEffect _dash = SKPathEffect.CreateDash([6, 4], 0);
    private readonly SKPathBuilder _herbivorePath = new();
    private readonly SKPathBuilder _predatorPath = new();
    private PopulationHistorySnapshot? _snapshot;
    internal int MaximumCount { get; private set; } = 1;

    internal PopulationHistoryChart() => _predators.PathEffect = _dash;
    internal void SetSnapshot(PopulationHistorySnapshot snapshot)
    {
        _snapshot = snapshot;
        MaximumCount = 1;
        foreach (var sample in snapshot.Samples)
            MaximumCount = Math.Max(MaximumCount, Math.Max(sample.HerbivoreCount, sample.PredatorCount));
    }

    internal bool Draw(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(new SKColor(21, 27, 36));
        if (width < 100 || height < 80) return false;
        var left = 24f; var right = width - 16f; var top = 12f; var bottom = height - 20f;
        canvas.DrawLine(left, top, left, bottom, _axis);
        canvas.DrawLine(left, bottom, right, bottom, _axis);
        var samples = _snapshot?.Samples;
        if (samples is null || samples.Count == 0) return true;
        var first = samples[0].Tick; var range = checked(samples[^1].Tick - first);
        _herbivorePath.Reset(); _predatorPath.Reset();
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            // Subtract exact Int64 ticks before converting, preserving narrow ranges near MaxValue.
            var x = range == 0 ? left : (float)(left + (sample.Tick - first) / (double)range * (right - left));
            var h = (float)(bottom - sample.HerbivoreCount / (double)MaximumCount * (bottom - top));
            var p = (float)(bottom - sample.PredatorCount / (double)MaximumCount * (bottom - top));
            if (index == 0) { _herbivorePath.MoveTo(x, h); _predatorPath.MoveTo(x, p); }
            else { _herbivorePath.LineTo(x, h); _predatorPath.LineTo(x, p); }
            if (samples.Count == 1)
            {
                canvas.DrawCircle(x, h, 3, _herbivores);
                canvas.DrawCircle(x, p, 2, _predators);
            }
        }
        using var herbivorePath = _herbivorePath.Snapshot();
        using var predatorPath = _predatorPath.Snapshot();
        canvas.DrawPath(herbivorePath, _herbivores);
        // Dashed overlap leaves both species visible when both lie on the zero baseline.
        canvas.DrawPath(predatorPath, _predators);
        return true;
    }

    public void Dispose()
    {
        _axis.Dispose(); _herbivores.Dispose(); _predators.Dispose(); _dash.Dispose();
        _herbivorePath.Dispose(); _predatorPath.Dispose();
    }
}

internal sealed class PopulationHistoryWindow : Window
{
    private readonly Func<PopulationHistory> _history;
    private readonly Func<string?> _observationError;
    private readonly PopulationHistoryChart _chart = new();
    private readonly SKElement _canvas = new();
    private readonly TextBlock _summary = Label();
    private readonly TextBlock _diversity = Label();
    private readonly TextBlock _axes = Label();
    private readonly TextBlock _status = Label();
    private PopulationHistory? _displayedHistory;
    private long _displayedRevision = -1;
    private string? _displayedError;
    private bool _closed;
    private bool _viewportRejected;

    internal PopulationHistoryWindow(Func<PopulationHistory> history, Func<string?> observationError)
    {
        _history = history; _observationError = observationError;
        Title = "EvoWorld — Population history"; Width = 760; Height = 480; MinWidth = 480; MinHeight = 400;
        Background = new SolidColorBrush(Color.FromRgb(21, 27, 36));
        var panel = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel();
        var toolbar = new DockPanel();
        var export = new Button { Content = "Export history CSV...", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 6) };
        export.Click += Export_Click;
        toolbar.Children.Add(export);
        header.Children.Add(toolbar);
        foreach (var text in new[] { _summary, _diversity, _axes }) header.Children.Add(text);
        var headerScroll = new ScrollViewer { Content = header, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        DockPanel.SetDock(headerScroll, Dock.Top); panel.Children.Add(headerScroll);
        var legend = Label(); legend.Text = "Herbivores: cyan solid · Predators: purple dashed · Vertical axis: living organisms";
        DockPanel.SetDock(legend, Dock.Top); panel.Children.Add(legend);
        var statusScroll = new ScrollViewer { Content = _status, MaxHeight = 60, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        DockPanel.SetDock(statusScroll, Dock.Bottom); panel.Children.Add(statusScroll);
        _canvas.PaintSurface += Paint;
        _canvas.SizeChanged += (_, _) => _canvas.InvalidateVisual();
        panel.Children.Add(_canvas); Content = panel;
        try { Refresh(); }
        catch { _canvas.PaintSurface -= Paint; _chart.Dispose(); throw; }
    }

    private static TextBlock Label() => new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
    internal void Refresh()
    {
        if (_closed) return;
        var history = _history(); var error = _observationError();
        if (ReferenceEquals(history, _displayedHistory) && history.Revision == _displayedRevision && error == _displayedError) return;
        var snapshot = history.Capture(); // Only when opened, samples change, or Reset installs a run.
        _chart.SetSnapshot(snapshot); _displayedHistory = history; _displayedRevision = history.Revision; _displayedError = error;
        var last = snapshot.Samples.Count == 0 ? (PopulationHistorySample?)null : snapshot.Samples[^1];
        _summary.Text = $"Latest sample tick: {last?.Tick.ToString(CultureInfo.InvariantCulture) ?? "none"} | Interval: 100 committed ticks | Retained: {history.Count}"
            + $" | Missed boundaries: {history.TotalMissedSamplingBoundaries}"
            + (history.DroppedSamples > 0 ? $" | HISTORY TRUNCATED: {history.DroppedSamples} dropped" : "");
        _diversity.Text = $"Mean parameter variance — Herbivores: {Value(last?.Herbivores.MeanParameterVariance)} | Predators: {Value(last?.Predators.MeanParameterVariance)}"
            + $" | Neural genome {(snapshot.Metadata.NeuralGenomeActive ? "active" : "inactive")}; dispersion does not prove adaptation.";
        _axes.Text = $"Horizontal axis: Tick ({(snapshot.Samples.Count == 0 ? "none" : snapshot.Samples[0].Tick.ToString(CultureInfo.InvariantCulture))}–{last?.Tick.ToString(CultureInfo.InvariantCulture) ?? "none"})"
            + $" | Count range: 0–{_chart.MaximumCount}";
        _status.Text = error ?? "Export each run before Reset; Reset discards its in-memory history.";
        _canvas.InvalidateVisual();
    }

    private static string Value(double? value) => value?.ToString("G5", CultureInfo.InvariantCulture) ?? "N/A (empty species)";
    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        try
        {
            if (!_chart.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height))
            {
                _viewportRejected = true; _status.Text = "History chart viewport is too small.";
            }
            else if (_viewportRejected)
            {
                _viewportRejected = false;
                _status.Text = _displayedError ?? "Export each run before Reset; Reset discards its in-memory history.";
            }
        }
        catch (Exception exception) { _status.Text = $"History drawing failed: {exception.Message}"; }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", DefaultExt = ".csv", AddExtension = true, FileName = "EvoWorld-history.csv" };
            if (dialog.ShowDialog(this) != true) return;
            ExportTo(dialog.FileName);
        }
        catch (Exception exception) { _status.Text = $"History export dialog failed: {exception.Message}"; }
    }

    internal void ExportTo(string? target)
    {
        if (target is null) return; // Cancellation is a no-op, including no snapshot allocation.
        try
        {
            var snapshot = _history().Capture();
            PopulationHistoryCsv.Export(target, snapshot);
            _status.Text = $"Exported {snapshot.Samples.Count} retained samples.";
        }
        catch (Exception exception) { _status.Text = $"History export failed: {exception.Message}"; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true; _canvas.PaintSurface -= Paint; _chart.Dispose(); base.OnClosed(e);
    }
}
