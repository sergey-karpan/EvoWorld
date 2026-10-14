using System.Collections;
using System.Reflection;
using EvoWorld.Core;

namespace EvoWorld.Desktop;

internal sealed record HistoryRunMetadata(ulong Seed, ControllerMode Controller, double NeuralMutationAmplitude,
    int InitialPredatorCount, double TickDurationSeconds, long SamplingIntervalTicks, string CoreVersion, string RuntimeVersion)
{
    internal bool NeuralGenomeActive => Controller == ControllerMode.Neural;
}

internal readonly record struct PopulationHistorySample(long Tick, double? ModelTimeSeconds,
    int OrganismCount, int HerbivoreCount, int PredatorCount, int FoodCount,
    long TotalBirths, long TotalDeaths, long TotalPredationEvents,
    long BirthsSincePreviousSample, long DeathsSincePreviousSample, long PredationEventsSincePreviousSample,
    long MissedSamplingBoundaries, SpeciesHistorySummary Herbivores, SpeciesHistorySummary Predators);

internal sealed record PopulationHistorySnapshot(HistoryRunMetadata Metadata, long DroppedSamples,
    long TotalMissedSamplingBoundaries, IReadOnlyList<PopulationHistorySample> Samples);

// One UI-thread-owned run. Storage grows on samples only, then retains a bounded FIFO.
// Neither records nor metadata retain a simulation, world, organism or genome.
internal sealed class PopulationHistory : IReadOnlyList<PopulationHistorySample>
{
    internal const long SamplingIntervalTicks = 100;
    internal const int MaximumSamples = 50_000;
    private readonly List<PopulationHistorySample> _samples;
    private readonly int _capacity;
    private int _start;
    internal HistoryRunMetadata Metadata { get; }
    internal long DroppedSamples { get; private set; }
    internal long TotalMissedSamplingBoundaries { get; private set; }
    internal long Revision { get; private set; }
    public int Count => _samples.Count;
    public PopulationHistorySample this[int index] => index >= 0 && index < Count
        ? _samples[(_start + index) % Count] : throw new ArgumentOutOfRangeException(nameof(index));

    internal PopulationHistory(Simulation simulation, int capacity = MaximumSamples)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (capacity <= 0 || capacity > MaximumSamples) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _samples = new(Math.Min(64, capacity));
        var settings = simulation.Settings;
        Metadata = new(settings.Seed, settings.EnableNeuralControl ? ControllerMode.Neural : ControllerMode.RuleBased,
            settings.EnableNeuralControl ? settings.NeuralWeightMutationAmplitude : 0,
            settings.InitialPredatorCount, settings.TickDurationSeconds, SamplingIntervalTicks,
            typeof(Simulation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(Simulation).Assembly.GetName().Version?.ToString() ?? "unknown", Environment.Version.ToString());
        Observe(simulation);
    }

    internal bool Observe(Simulation simulation)
    {
        var tick = simulation.TickCount;
        if (tick != 0 && tick % SamplingIntervalTicks != 0) return false;
        PopulationHistorySample? previous = Count == 0 ? null : this[Count - 1];
        if (previous?.Tick == tick) return false;
        if (previous is { } old && tick < old.Tick) throw new InvalidOperationException("History ticks must increase.");
        // Capture one advancement version for both statistics and scalar/genome observations.
        var statistics = simulation.GetStatistics(out var snapshot);
        if (snapshot.TickCount != tick || statistics.TickCount != tick
            || (long)statistics.HerbivoreCount + statistics.PredatorCount != statistics.OrganismCount
            || snapshot.World.Organisms.Count != statistics.OrganismCount)
            throw new InvalidOperationException("Inconsistent committed history snapshot.");
        var births = Delta(statistics.TotalBirths, previous?.TotalBirths);
        var deaths = Delta(statistics.TotalDeaths, previous?.TotalDeaths);
        var predations = Delta(statistics.TotalPredationEvents, previous?.TotalPredationEvents);
        var missed = previous is { } last ? checked((tick - last.Tick) / SamplingIntervalTicks - 1) : 0;
        var seconds = tick * Metadata.TickDurationSeconds;
        var sample = new PopulationHistorySample(tick, double.IsFinite(seconds) ? seconds : null,
            statistics.OrganismCount, statistics.HerbivoreCount, statistics.PredatorCount, statistics.FoodCount,
            statistics.TotalBirths, statistics.TotalDeaths, statistics.TotalPredationEvents,
            births, deaths, predations, missed,
            HistoryStatistics.Calculate(snapshot.World.Organisms, Species.Herbivore, Metadata.NeuralGenomeActive),
            HistoryStatistics.Calculate(snapshot.World.Organisms, Species.Predator, Metadata.NeuralGenomeActive));
        if (sample.Herbivores.Speed.Count != sample.HerbivoreCount || sample.Predators.Speed.Count != sample.PredatorCount)
            throw new InvalidOperationException("History species aggregates must match population counts.");
        var dropped = checked(DroppedSamples + (Count == _capacity ? 1 : 0));
        var totalMissed = checked(TotalMissedSamplingBoundaries + missed);
        var revision = checked(Revision + 1);
        // All calculations and any allocation finish before changing the retained history.
        if (Count < _capacity)
        {
            if (Count == _samples.Capacity) _samples.Capacity = Math.Min(_capacity, _samples.Capacity * 2);
            _samples.Add(sample);
        }
        else
        {
            _samples[_start] = sample;
            _start = (_start + 1) % _capacity;
        }
        DroppedSamples = dropped; TotalMissedSamplingBoundaries = totalMissed; Revision = revision;
        return true;
    }

    private static long Delta(long current, long? previous)
    {
        if (current < 0) throw new InvalidOperationException("History counters cannot be negative.");
        var delta = previous is long value ? checked(current - value) : 0;
        if (delta < 0) throw new InvalidOperationException("History counters must not decrease.");
        return delta;
    }

    internal PopulationHistorySnapshot Capture() => new(Metadata, DroppedSamples, TotalMissedSamplingBoundaries,
        Array.AsReadOnly(this.ToArray()));
    public IEnumerator<PopulationHistorySample> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
