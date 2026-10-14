using System.Globalization;
using EvoWorld.Core;

namespace EvoWorld.Desktop;

internal readonly record struct TrailPoint(long TickCount, double X, double Y);

// Scalar history deliberately does not retain an organism's genome or a world.
internal sealed record ObservedOrganism(int Id, long TickCount, double X, double Y, double Energy,
    double Speed, double Metabolism, double SensingRadius, double SteeringRate, double Heading);

internal sealed class OrganismInspection
{
    internal const int Capacity = 500;
    private readonly TrailPoint[] _trail = new TrailPoint[Capacity];
    private int _start;
    private long _observedTick = -1;
    internal int? SelectedId { get; private set; }
    internal int Count { get; private set; }
    internal bool IsAlive { get; private set; }
    internal ObservedOrganism? LastAlive { get; private set; }
    internal OrganismDiagnosticSample? LastActed { get; private set; }
    internal TrailPoint this[int index] => index >= 0 && index < Count
        ? _trail[(_start + index) % Capacity] : throw new ArgumentOutOfRangeException(nameof(index));

    internal void Clear(Simulation simulation)
    {
        simulation.DisableDiagnostics();
        SelectedId = null;
        Count = _start = 0;
        _observedTick = -1;
        IsAlive = false;
        LastAlive = null;
        LastActed = null;
    }

    internal void Select(Simulation simulation, int? id)
    {
        if (id is < 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (SelectedId == id) return;
        Clear(simulation);
        if (id is not int selected) return;
        SelectedId = selected;
        simulation.EnableDiagnosticsFor(selected);
        Observe(simulation); // Seed only the current committed position.
    }

    internal void Observe(Simulation simulation)
    {
        if (SelectedId is not int id) return;
        var snapshot = simulation.State;
        if (_observedTick == snapshot.TickCount) return;
        _observedTick = snapshot.TickCount;
        var organism = snapshot.World.Organisms.FirstOrDefault(o => o.Id == id);
        IsAlive = organism is not null;
        if (organism is not null)
        {
            LastAlive = new(id, snapshot.TickCount, organism.X, organism.Y, organism.Energy,
                organism.Speed, organism.Metabolism, organism.SensingRadius, organism.SteeringRate, organism.HeadingRadians);
            var slot = (_start + Count) % Capacity;
            _trail[slot] = new(snapshot.TickCount, organism.X, organism.Y);
            if (Count == Capacity) _start = (_start + 1) % Capacity;
            else Count++;
        }
        var sample = simulation.LastDiagnosticSample;
        if (sample is { DidAct: true } && sample.OrganismId == id && sample.TickCount == snapshot.TickCount)
            LastActed = sample;
    }

    internal string UnavailableReason(Simulation simulation)
    {
        if (SelectedId is not int id) return "Organism not found";
        if (simulation.DiagnosticOrganismId != id) return "Diagnostics disabled";
        var sample = simulation.LastDiagnosticSample;
        if (sample is null) return "No sample yet";
        if (sample.OrganismId != id || sample.TickCount != simulation.TickCount) return "unexpected";
        if (sample.IsRuleBasedMode) return "Rule-based controller";
        if (!IsAlive) return "Organism not found";
        if (!sample.DidAct) return "Did not act this tick";
        return "unexpected";
    }

    internal string Format(Simulation simulation)
    {
        if (SelectedId is not int id) return "";
        var heading = $"Organism #{id} — {(IsAlive ? "alive" : LastAlive is null ? "not found" : "deceased")} | Tick {simulation.TickCount}";
        var state = LastAlive is { } o ? string.Create(CultureInfo.InvariantCulture,
            $"{(IsAlive ? "State" : "Historical last alive state")} at tick {o.TickCount}: X {o.X:G5}, Y {o.Y:G5}, Energy {o.Energy:G5}\n" +
            $"Inherited Speed {o.Speed:G5} world units/s | Metabolism {o.Metabolism:G5}\n" +
            $"SensingRadius {o.SensingRadius:G5} | SteeringRate {o.SteeringRate:G5} rad/s | Heading {o.Heading:G5} rad") : "Organism not found";
        var sample = simulation.LastDiagnosticSample;
        var commands = sample is { DidAct: true, IsNeuralMode: true } && sample.OrganismId == id && sample.TickCount == simulation.TickCount
            ? FormatCommands(sample) : $"Neural outputs / Applied rotation / Effective speed: N/A ({UnavailableReason(simulation)})";
        if (!IsAlive && LastActed is { } historical)
            commands += $"\nLast acted at tick {historical.TickCount}: " + (historical.IsNeuralMode
                ? FormatCommands(historical) : "Neural commands: N/A (Rule-based controller)");
        return $"{heading}\n{state}\nController: {(simulation.Settings.EnableNeuralControl ? "Neural" : "Rule-based")} | Trail: {Count}/{Capacity}\n{commands}";
    }

    private static string FormatCommands(OrganismDiagnosticSample sample) => string.Create(CultureInfo.InvariantCulture,
        $"Commands at tick {sample.TickCount}: Neural output[0] {sample.NeuralOutputSteering:G5}, output[1] {sample.NeuralOutputSpeed:G5}\n" +
        $"Applied rotation {sample.AppliedRotationRadians:G5} rad/tick | Effective speed {sample.EffectiveSpeed:G5} world units/s (before reflection)");
}
