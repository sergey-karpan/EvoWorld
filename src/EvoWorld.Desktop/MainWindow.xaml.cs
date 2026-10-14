using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Threading;
using System.Windows.Input;
using EvoWorld.Core;
using SkiaSharp.Views.Desktop;

namespace EvoWorld.Desktop;

public partial class MainWindow : Window
{
    private readonly DesktopExperiment _experiment;
    private readonly WorldRenderer _renderer = new();
    private readonly DispatcherTimer _timer;
    private Simulation ActiveSimulation => _experiment.Simulation;
    private bool _failed;
    private bool _closed;
    private string _playbackStatus = "Ready";
    private int _canvasWidth;
    private int _canvasHeight;
    private PopulationHistoryWindow? _historyWindow;

    public MainWindow() : this(new DesktopExperiment())
    {
    }

    internal MainWindow(DesktopExperiment experiment)
    {
        _experiment = experiment;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(ActiveSimulation.Settings.TickDurationSeconds)
        };
        _timer.Tick += Timer_Tick;
        InitializeComponent();
        PredatorSelector.ItemsSource = ExperimentSettings.PredatorChoices.ToArray();
        PredatorSelector.SelectedItem = _experiment.PendingOptions.InitialPredatorCount;
        MutationSelector.ItemsSource = ExperimentSettings.MutationChoices.ToArray();
        MutationSelector.SelectedItem = _experiment.PendingOptions.MutationAmplitude;
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
            _experiment.Reset();
        }
        catch (Exception exception) { ShowError("Reset failed", exception); return; }
        _failed = false;
        UpdateControls("Ready");
        Viewport.InvalidateVisual();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_closed || _failed || !_timer.IsEnabled) return;
        try
        {
            _experiment.AdvanceTick(); // One fixed step, no catch-up.
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
            var statistics = ActiveSimulation.GetStatistics(out var snapshot);
            _canvasWidth = e.Info.Width;
            _canvasHeight = e.Info.Height;
            var rendered = _renderer.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height, snapshot.World, statistics, SelectedTrait, _experiment.Inspection);
            StatusText.Text = rendered ? _playbackStatus : "World cannot be displayed at this viewport scale.";
        }
        catch (Exception exception) { ShowError("Drawing stopped", exception); }
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Viewport.InvalidateVisual();

    private void History_Click(object sender, RoutedEventArgs e)
    {
        PopulationHistoryWindow? window = null;
        try
        {
            if (_historyWindow is not null) { _historyWindow.Activate(); return; }
            window = new PopulationHistoryWindow(() => _experiment.History, () => _experiment.HistoryObservationError) { Owner = this };
            window.Closed += (_, _) => _historyWindow = null;
            window.Show(); _historyWindow = window;
        }
        catch (Exception exception)
        {
            window?.Close();
            HistoryErrorText.Text = $"History view failed: {exception.Message}"; HistoryErrorText.Visibility = Visibility.Visible;
        }
    }

    private void WorldArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Bound the inspector by the space remaining below the toolbar, preserving
        // a visible canvas even at the minimum window size. Long history scrolls.
        InspectorScroll.MaxHeight = Math.Max(24, Math.Min(185, e.NewSize.Height * 0.45 - 16));
    }

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_closed || _failed || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0) return;
        var position = e.GetPosition(Viewport);
        var snapshot = ActiveSimulation.State;
        var id = OrganismHitTesting.Find(snapshot.World, _canvasWidth, _canvasHeight, position.X, position.Y,
            _canvasWidth / Viewport.ActualWidth, _canvasHeight / Viewport.ActualHeight);
        _experiment.Inspection.Select(ActiveSimulation, id);
        UpdateReadout();
        Viewport.InvalidateVisual();
    }

    private void ExperimentSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Selection events can occur before InitializeComponent has built every control.
        if (MutationSelector is null || PredatorSelector is null || ConfigurationText is null
            || MutationSelector.SelectedItem is not double amplitude || PredatorSelector.SelectedItem is not int predators) return;
        var controller = ControllerSelector.SelectedIndex == 1 ? ControllerMode.Neural : ControllerMode.RuleBased;
        _experiment.Select(new ExperimentOptions(controller, amplitude, predators));
        MutationSelector.IsEnabled = controller == ControllerMode.Neural;
        UpdateConfigurationReadout();
    }

    private void UpdateConfigurationReadout()
    {
        var settings = ActiveSimulation.Settings;
        var mode = settings.EnableNeuralControl ? "Neural" : "Rule-based";
        var enabled = settings.EnableNeuralControl ? "On" : "Off";
        var mutation = settings.EnableNeuralControl && settings.NeuralWeightMutationAmplitude > 0 ? "enabled" : "off";
        var pending = _experiment.HasPendingChanges ? " | Pending Reset" : "";
        ConfigurationText.Text = string.Create(CultureInfo.InvariantCulture,
            $"Active: {mode} | Neural: {enabled} | Mutation setting: {settings.NeuralWeightMutationAmplitude:G3} ({mutation}) | Initial predators: {settings.InitialPredatorCount}{pending}");
    }

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
        UpdateConfigurationReadout();
        StatusText.Text = status;
    }

    private void UpdateReadout()
    {
        var statistics = ActiveSimulation.GetStatistics();
        TickText.Text = $"Tick: {statistics.TickCount} | Organisms: {statistics.OrganismCount} | Food: {statistics.FoodCount}"
            + $" | Herbivores: {statistics.HerbivoreCount} | Predators: {statistics.PredatorCount} | Predation: {statistics.TotalPredationEvents}";
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
        InspectorPanel.Visibility = _experiment.Inspection.SelectedId is null ? Visibility.Collapsed : Visibility.Visible;
        InspectorText.Text = _experiment.Inspection.Format(ActiveSimulation);
        HistoryErrorText.Text = _experiment.HistoryObservationError ?? "";
        HistoryErrorText.Visibility = _experiment.HistoryObservationError is null ? Visibility.Collapsed : Visibility.Visible;
        try { _historyWindow?.Refresh(); }
        catch (Exception exception) { HistoryErrorText.Text = $"History view failed: {exception.Message}"; HistoryErrorText.Visibility = Visibility.Visible; }
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
        _historyWindow?.Close();
        _renderer.Dispose();
        base.OnClosed(e);
    }
}
