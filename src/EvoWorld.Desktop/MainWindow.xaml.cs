using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Threading;
using EvoWorld.Core;
using SkiaSharp.Views.Desktop;

namespace EvoWorld.Desktop;

public partial class MainWindow : Window
{
    private readonly SimulationSettings _settings = SimulationSettings.CreateDefault();
    private readonly WorldRenderer _renderer = new();
    private readonly DispatcherTimer _timer;
    private Simulation _simulation;
    private bool _failed;
    private bool _closed;
    private string _playbackStatus = "Ready";

    public MainWindow()
    {
        _simulation = new Simulation(_settings);
        _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(_settings.TickDurationSeconds)
        };
        _timer.Tick += Timer_Tick;
        InitializeComponent();
        TraitSelector.ItemsSource = Enum.GetValues<ColorTrait>();
        TraitSelector.SelectedItem = ColorTrait.None;
        UpdateControls("Ready");
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _failed || _timer.IsEnabled) return;
        _timer.Start(); // First tick occurs on the next callback.
        UpdateControls("Running");
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        UpdateControls("Paused");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        try
        {
            _simulation = new Simulation(_settings);
            _failed = false;
            UpdateControls("Ready");
            Viewport.InvalidateVisual();
        }
        catch (Exception exception) { ShowError("Reset failed", exception); }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_closed || _failed || !_timer.IsEnabled) return;
        try
        {
            _simulation.AdvanceTick(); // One fixed step, no catch-up.
            UpdateReadout();
            Viewport.InvalidateVisual();
        }
        catch (Exception exception) { ShowError("Simulation stopped", exception); }
    }

    private void Viewport_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_closed || _failed) return;
        try
        {
            var statistics = _simulation.GetStatistics(out var snapshot);
            var rendered = _renderer.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height, snapshot.World, statistics, SelectedTrait);
            StatusText.Text = rendered ? _playbackStatus : "World cannot be displayed at this viewport scale.";
        }
        catch (Exception exception) { ShowError("Drawing stopped", exception); }
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Viewport.InvalidateVisual();

    private ColorTrait SelectedTrait => TraitSelector.SelectedItem is ColorTrait trait ? trait : ColorTrait.None;

    private void TraitSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Viewport is null || StatisticsText is null) return;
        UpdateReadout();
        Viewport.InvalidateVisual();
    }

    private void UpdateControls(string status)
    {
        _playbackStatus = status;
        StartButton.IsEnabled = !_failed && !_timer.IsEnabled;
        PauseButton.IsEnabled = _timer.IsEnabled;
        UpdateReadout();
        StatusText.Text = status;
    }

    private void UpdateReadout()
    {
        var statistics = _simulation.GetStatistics();
        TickText.Text = $"Tick: {statistics.TickCount} | Organisms: {statistics.OrganismCount} | Food: {statistics.FoodCount}";
        var trait = SelectedTrait;
        var moments = trait switch
        {
            ColorTrait.Metabolism => statistics.Metabolism,
            ColorTrait.SensingRadius => statistics.SensingRadius,
            ColorTrait.SteeringRate => statistics.SteeringRate,
            _ => statistics.Speed
        };
        var label = trait is ColorTrait.None or ColorTrait.Energy ? "Speed" : trait.ToString();
        StatisticsText.Text = string.Create(CultureInfo.InvariantCulture,
            $"{label}: {moments.Mean:G4} ± {moments.StandardDeviation:G4} | Births: {statistics.TotalBirths} | Deaths: {statistics.TotalDeaths}");
    }

    private void ShowError(string context, Exception exception)
    {
        _timer.Stop();
        _failed = true;
        UpdateControls($"Error: {context}. {exception.Message} Reset to try again.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _renderer.Dispose();
        base.OnClosed(e);
    }
}
