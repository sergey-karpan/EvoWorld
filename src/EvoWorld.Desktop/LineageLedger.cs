using System.Globalization;
using EvoWorld.Core;

namespace EvoWorld.Desktop;

[Flags]
internal enum LineageCompleteness
{
    Complete = 0, MissingBirthTick = 1, MissingDeathTick = 2, CapacityTruncated = 4, ObservationGap = 8
}

internal sealed record LineageRecord(int OrganismId, Species Species, int? ParentId, long? BirthTick,
    long? DeathTick, bool IsAliveAtLastObservation, int? DirectChildCount, LineageCompleteness CompletenessFlag);
internal sealed record LineageMetadata(ulong Seed, bool Neural, double MutationAmplitude, int InitialPredatorCount, double TimestepSeconds);
internal sealed record LineageSnapshot(LineageMetadata Metadata, long LastObservedTick, long DroppedLineageRecords,
    IReadOnlyList<LineageRecord> Records)
{
    internal bool CapacityTruncated => DroppedLineageRecords != 0;
}

internal sealed class LineageLedgerCapacityException : InvalidOperationException
{
    internal LineageLedgerCapacityException() : base("Lineage capacity reached; no deceased record can be evicted.") { }
}

// UI-thread owned, scalar-only immutable publication. Order is first observation order;
// each committed snapshot is processed once, independently of population-history sampling.
internal sealed class LineageLedger
{
    internal const int MaxLineageRecords = 100_000;
    private readonly int _capacity;
    private LineageSnapshot _state;
    internal long LastObservedTick => _state.LastObservedTick;
    internal long DroppedLineageRecords => _state.DroppedLineageRecords;
    internal bool CapacityTruncated => _state.CapacityTruncated;

    internal LineageLedger(Simulation simulation, int capacity = MaxLineageRecords)
    {
        if (capacity <= 0 || capacity > MaxLineageRecords) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        var s = simulation.Settings;
        _state = new(new(s.Seed, s.EnableNeuralControl, s.EnableNeuralControl ? s.NeuralWeightMutationAmplitude : 0,
            s.InitialPredatorCount, s.TickDurationSeconds), -1, 0, Array.AsReadOnly(Array.Empty<LineageRecord>()));
        Observe(simulation);
    }

    internal LineageSnapshot Capture() => _state;
    internal LineageRecord? Find(int id) => _state.Records.FirstOrDefault(r => r.OrganismId == id);

    internal bool Observe(Simulation simulation)
    {
        var committed = simulation.State;
        var tick = committed.TickCount;
        var prior = _state;
        if (tick == prior.LastObservedTick) return false;
        if (tick < prior.LastObservedTick) throw new InvalidOperationException("Lineage observations cannot go backwards.");
        var initial = prior.LastObservedTick == -1;
        // Subtracting nonnegative ticks avoids LastObservedTick + 1 overflow at Int64.MaxValue.
        var gap = !initial && tick - prior.LastObservedTick != 1;
        var living = committed.World.Organisms.ToDictionary(o => o.Id);
        var records = new List<LineageRecord>(prior.Records.Count);
        var indices = new Dictionary<int, int>(prior.Records.Count);
        foreach (var old in prior.Records)
        {
            var record = old;
            if (gap) record = record with { DirectChildCount = null, CompletenessFlag = record.CompletenessFlag | LineageCompleteness.ObservationGap };
            if (old.IsAliveAtLastObservation && !living.ContainsKey(old.OrganismId))
                record = record with { IsAliveAtLastObservation = false, DeathTick = gap ? null : tick,
                    CompletenessFlag = record.CompletenessFlag | (gap ? LineageCompleteness.MissingDeathTick : LineageCompleteness.Complete) };
            if (!old.IsAliveAtLastObservation && living.ContainsKey(old.OrganismId))
                throw new InvalidOperationException("A deceased organism ID cannot reappear.");
            indices.Add(record.OrganismId, records.Count); records.Add(record);
        }
        foreach (var organism in committed.World.Organisms)
        {
            if (indices.TryGetValue(organism.Id, out var existing))
            {
                if (records[existing].ParentId != organism.ParentId || records[existing].Species != organism.Species)
                    throw new InvalidOperationException("Immutable lineage provenance changed.");
                continue;
            }
            var knownBirth = initial ? tick == 0 : !gap;
            var record = new LineageRecord(organism.Id, organism.Species, organism.ParentId, knownBirth ? tick : null,
                null, true, knownBirth ? 0 : null, knownBirth ? LineageCompleteness.Complete
                    : LineageCompleteness.MissingBirthTick | LineageCompleteness.ObservationGap);
            indices.Add(record.OrganismId, records.Count); records.Add(record);
            if (!initial && organism.ParentId is int parent && indices.TryGetValue(parent, out var parentIndex))
            {
                var p = records[parentIndex];
                records[parentIndex] = p with { DirectChildCount = p.DirectChildCount is int count ? checked(count + 1) : null };
            }
        }
        var excess = Math.Max(0, records.Count - _capacity);
        var dropped = checked(prior.DroppedLineageRecords + excess);
        if (excess != 0)
        {
            // Do not mutate the prior publication even if no safe eviction exists.
            var retained = new List<LineageRecord>(_capacity);
            foreach (var record in records)
            {
                if (excess > 0 && !record.IsAliveAtLastObservation) { excess--; continue; }
                retained.Add(record);
            }
            if (excess != 0) throw new LineageLedgerCapacityException();
            records = retained;
        }
        var next = new LineageSnapshot(prior.Metadata, tick, dropped, Array.AsReadOnly(records.ToArray()));
        _state = next; // Sole publication; all calculations, capacity checks and allocations completed.
        return true;
    }

    internal string Format(int id)
    {
        var r = Find(id);
        var text = r is null ? "Species: Unknown | Parent: Unknown | Direct children: unavailable"
            : $"{(r.IsAliveAtLastObservation ? "Alive" : "Deceased")} at lineage observation tick {_state.LastObservedTick}: organism #{r.OrganismId} | Species: {r.Species} | Parent: "
                + (r.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "Founder") + " | Direct children: "
                + (r.DirectChildCount?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
                + $" | Completeness: {Flags(r.CompletenessFlag)}";
        return text + (CapacityTruncated ? $"\nLineage capacity truncated: {DroppedLineageRecords} deceased records dropped." : "");
    }

    internal static string Flags(LineageCompleteness flags)
    {
        if (flags == LineageCompleteness.Complete) return "Complete";
        return string.Join('|', new[] { LineageCompleteness.MissingBirthTick, LineageCompleteness.MissingDeathTick,
            LineageCompleteness.CapacityTruncated, LineageCompleteness.ObservationGap }.Where(f => (flags & f) != 0));
    }
}
