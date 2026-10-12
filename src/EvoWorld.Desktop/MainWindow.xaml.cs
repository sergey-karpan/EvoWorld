using System.Windows;
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
            TickText.Text = $"Tick: {_simulation.TickCount}";
            Viewport.InvalidateVisual();
        }
        catch (Exception exception) { ShowError("Simulation stopped", exception); }
    }

    private void Viewport_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_closed || _failed) return;
        try
        {
            var snapshot = _simulation.State;
            var rendered = _renderer.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height, snapshot.World);
            StatusText.Text = rendered ? _playbackStatus : "World cannot be displayed at this viewport scale.";
        }
        catch (Exception exception) { ShowError("Drawing stopped", exception); }
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Viewport.InvalidateVisual();

    private void UpdateControls(string status)
    {
        _playbackStatus = status;
        StartButton.IsEnabled = !_failed && !_timer.IsEnabled;
        PauseButton.IsEnabled = _timer.IsEnabled;
        TickText.Text = $"Tick: {_simulation.TickCount}";
        StatusText.Text = status;
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
