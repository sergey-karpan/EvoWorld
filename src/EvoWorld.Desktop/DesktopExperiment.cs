using EvoWorld.Core;

namespace EvoWorld.Desktop;

// UI-independent ownership of selected versus applied configuration. Playback
// stays in MainWindow; selection never advances or changes the active simulation.
internal sealed class DesktopExperiment
{
    private sealed record ActiveRun(Simulation Simulation, ExperimentOptions Options);
    private readonly Func<SimulationSettings, Simulation> _createSimulation;
    private ActiveRun _active;
    internal OrganismInspection Inspection { get; } = new();

    internal Simulation Simulation => _active.Simulation;
    internal ExperimentOptions ActiveOptions => _active.Options;
    internal ExperimentOptions PendingOptions { get; private set; } = ExperimentOptions.Initial;
    internal bool HasPendingChanges => PendingOptions.Effective != ActiveOptions;

    // Narrow Desktop-only construction seam: tests can fail Reset construction
    // without modifying Core or requiring a WPF test framework.
    internal DesktopExperiment(Func<SimulationSettings, Simulation>? createSimulation = null)
    {
        _createSimulation = createSimulation ?? (static settings => new Simulation(settings));
        _active = Construct(PendingOptions);
    }

    internal void Select(ExperimentOptions options)
    {
        ExperimentSettings.Validate(options);
        PendingOptions = options;
    }

    internal void Reset()
    {
        var selected = PendingOptions;
        var replacement = Construct(selected);
        Inspection.Clear(_active.Simulation);
        _active = replacement; // Publish only after complete successful construction, even when selections match.
    }

    internal void AdvanceTick()
    {
        Simulation.AdvanceTick();
        Inspection.Observe(Simulation); // Only after successful publication; never during paint.
    }

    private ActiveRun Construct(ExperimentOptions selected)
    {
        var settings = ExperimentSettings.Create(selected);
        var simulation = _createSimulation(settings)
            ?? throw new InvalidOperationException("Simulation construction returned no instance.");
        return new ActiveRun(simulation, selected.Effective);
    }
}
