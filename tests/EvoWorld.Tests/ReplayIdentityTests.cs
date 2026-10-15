using EvoWorld.Core;
using EvoWorld.Desktop;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace EvoWorld.Tests;

public sealed class ReplayIdentityTests
{
    internal static SimulationSettings Settings() => ExperimentSettings.Create(new(ControllerMode.Neural, 0.1, 5));

    [Fact]
    public void ExactDesktopSettingsReproduceHerbivore36Parent9WithinReviewedBirthTolerance()
    {
        var settings = Settings(); var baseline = SimulationSettings.CreateDefault();
        Assert.Equal(42UL, settings.Seed); Assert.True(settings.EnableNeuralControl);
        Assert.Equal(0.1, settings.NeuralWeightMutationAmplitude); Assert.Equal(5, settings.InitialPredatorCount);
        Assert.Equal(20, settings.InitialOrganismCount); Assert.Equal(0.5, settings.InitialNeuralWeightVariationAmplitude);
        foreach (var property in typeof(SimulationSettings).GetProperties())
        {
            if (property.Name is nameof(SimulationSettings.EnableNeuralControl) or nameof(SimulationSettings.NeuralWeightMutationAmplitude)
                or nameof(SimulationSettings.InitialPredatorCount)) continue;
            Assert.Equal(property.GetValue(baseline), property.GetValue(settings));
        }
        var simulation = new Simulation(settings);
        for (var tick = 1; tick <= 2032; tick++)
        {
            simulation.AdvanceTick(); var target = simulation.State.World.Organisms.FirstOrDefault(o => o.Id == 36);
            if (target is null) continue;
            Assert.Equal(Species.Herbivore, target.Species); Assert.Equal(9, target.ParentId);
            Assert.InRange(simulation.TickCount, 2022, 2032); return;
        }
        Assert.Fail("Identity gate: organism #36 did not appear by tick 2032; stop research, do not retune settings.");
    }
}

internal sealed record ResearchRow(OrganismResearchSample Sample, int Population, int Herbivores, int FoodCount,
    long PredationCount, double? PathTurn);
internal sealed record ReplayResult(SimulationSettings Settings, long BirthTick, long LastAliveTick, long EndTick,
    long FinalPredationCount, double[] Weights, string GenomeDigest, IReadOnlyList<ResearchRow> Rows,
    int ComparedSnapshots, double ReplaySeconds);

// A test-side research harness, not a new product persistence format or UI.
internal static class EnergyResearch
{
    internal static readonly Lazy<ReplayResult> Replay = new(Run);
    internal const int ArtifactLimitBytes = 50_000_000;
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(30);

    internal static void CheckBudget(Stopwatch watch)
    {
        if (watch.Elapsed > Budget) throw new TimeoutException("Research exceeded the 30-minute wall-clock budget; no reports may be published.");
    }

    private static ReplayResult Run()
    {
        var watch = Stopwatch.StartNew(); var settings = ReplayIdentityTests.Settings();
        var reference = new Simulation(settings);
        // Reference has all diagnostics disabled. The second run enables both
        // existing output diagnostics and the optional research capture.
        var hashes = new List<byte[]>(); long birth = -1, lastAlive = -1;
        double[]? weights = null;
        using var hasher = new StateHasher();
        hashes.Add(hasher.Hash(reference));
        while (true)
        {
            CheckBudget(watch); reference.AdvanceTick();
            var target = reference.State.World.Organisms.FirstOrDefault(o => o.Id == 36);
            if (target is not null)
            {
                if (birth == -1)
                {
                    birth = reference.TickCount;
                    Assert.InRange(birth, 2022, 2032); Assert.Equal(Species.Herbivore, target.Species); Assert.Equal(9, target.ParentId);
                    weights = target.Brain.Weights.ToArray();
                }
                lastAlive = reference.TickCount;
                Assert.Equal(weights!, target.Brain.Weights.ToArray());
            }
            hashes.Add(hasher.Hash(reference));
            if (birth < 0 && reference.TickCount > 2032) Assert.Fail("Identity gate failed; do not retune or substitute an organism.");
            if (birth >= 0 && target is null) break;
        }
        var observed = new Simulation(settings); observed.EnableDiagnosticsFor(36); observed.CaptureResearchTrace = true;
        var rows = new List<ResearchRow>();
        double? priorDx = null, priorDy = null;
        Assert.Equal(hashes[0], hasher.Hash(observed));
        while (observed.TickCount < reference.TickCount)
        {
            CheckBudget(watch); observed.AdvanceTick();
            Assert.Equal(hashes[checked((int)observed.TickCount)], hasher.Hash(observed));
            var sample = observed.LastResearchSample!;
            if (observed.TickCount < birth) continue;
            double? turn = null;
            if (sample.DidAct && sample.MovedX is double x && sample.MovedY is double y)
            {
                var dx = x - sample.BeforeX!.Value; var dy = y - sample.BeforeY!.Value;
                if (priorDx is double ax && priorDy is double ay && (ax != 0 || ay != 0) && (dx != 0 || dy != 0))
                {
                    var a = double.Hypot(ax, ay); var b = double.Hypot(dx, dy);
                    turn = Math.Atan2(ax / a * (dy / b) - ay / a * (dx / b), ax / a * (dx / b) + ay / a * (dy / b));
                }
                priorDx = dx; priorDy = dy;
            }
            else { priorDx = null; priorDy = null; }
            var stats = observed.GetStatistics();
            rows.Add(new(sample, stats.OrganismCount, stats.HerbivoreCount, stats.FoodCount, stats.TotalPredationEvents, turn));
        }
        Assert.NotNull(weights);
        var result = new ReplayResult(settings, birth, lastAlive, observed.TickCount, observed.GetStatistics().TotalPredationEvents,
            weights!, Digest(weights!), rows.AsReadOnly(), hashes.Count, watch.Elapsed.TotalSeconds);
        return result;
    }

    internal static string Digest(double[] weights)
    {
        var bytes = new byte[weights.Length * 8];
        for (var i = 0; i < weights.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(i * 8, 8), BitConverter.DoubleToInt64Bits(weights[i]));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    // Exact simulation-state bits, including genomes, parentage, RNG and IDs.
    // Observer samples themselves intentionally differ from the unobserved reference.
    private sealed class StateHasher : IDisposable
    {
        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _writer;
        internal StateHasher() { _writer = new(_stream, Encoding.UTF8, true); }
        internal byte[] Hash(Simulation simulation)
        {
            _stream.Position = 0; _stream.SetLength(0);
            var w = _writer; var state = simulation.State;
            w.Write(state.TickCount); w.Write(state.World.WorldWidth); w.Write(state.World.WorldHeight);
            w.Write(simulation.RandomState.State); w.Write(simulation.RandomState.Increment); w.Write(simulation.NextFoodId); w.Write(simulation.NextOrganismId);
            var statistics = simulation.GetStatistics();
            w.Write(statistics.TotalBirths); w.Write(statistics.TotalDeaths); w.Write(statistics.TotalPredationEvents);
            w.Write(state.World.Organisms.Count);
            foreach (var o in state.World.Organisms)
            {
                w.Write(o.Id); w.Write((int)o.Species); w.Write(o.ParentId.HasValue); if (o.ParentId is int parent) w.Write(parent);
                w.Write(o.X); w.Write(o.Y); w.Write(o.HeadingRadians); w.Write(o.Energy);
                w.Write(o.Speed); w.Write(o.Metabolism); w.Write(o.SensingRadius); w.Write(o.SteeringRate);
                foreach (var weight in o.Brain.Weights) w.Write(weight);
            }
            w.Write(state.World.FoodItems.Count);
            foreach (var food in state.World.FoodItems) { w.Write(food.Id); w.Write(food.X); w.Write(food.Y); w.Write(food.Energy); }
            w.Flush(); return SHA256.HashData(_stream.GetBuffer().AsSpan(0, checked((int)_stream.Length)));
        }
        public void Dispose() { _writer.Dispose(); _stream.Dispose(); }
    }

    internal const string TraceHeader = "Seed,OrganismId,ParentId,Tick,Status,DidAct,AliveAfterTick,BeforeX,BeforeY,BeforeHeading,EnergyBeforeDrain,PostDrainEnergy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,TargetKind,TargetId,TargetX,TargetY,TargetDistance,AppliedRotation,EffectiveSpeed,CommandedHeading,MovedX,MovedY,MovedHeading,ReflectedX,ReflectedY,Displacement,PathTurn,CommittedX,CommittedY,CommittedHeading,EnergyAfterTick,FoodPickedUp,FoodId,FoodEnergy,BirthsThisTick,Population,Herbivores,FoodCount,PredationCount,GenomeSha256";
    internal static string Value(object? value) => value switch
    {
        null => "", double d when double.IsFinite(d) => d.ToString("R", CultureInfo.InvariantCulture),
        double => throw new InvalidOperationException("Nonfinite research CSV value."),
        bool b => b ? "true" : "false", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString()!
    };
    internal static string TraceLine(ReplayResult replay, ResearchRow row)
    {
        var s = row.Sample; var i = s.Inputs; var o = s.Outputs;
        double? distance = s.TargetX is double tx ? double.Hypot(tx - s.BeforeX!.Value, s.TargetY!.Value - s.BeforeY!.Value) : null;
        double? displacement = s.MovedX is double mx ? double.Hypot(mx - s.BeforeX!.Value, s.MovedY!.Value - s.BeforeY!.Value) : null;
        object?[] fields = [replay.Settings.Seed, s.OrganismId, s.ParentId, s.TickCount, s.Status, s.DidAct, s.AliveAfterTick,
            s.BeforeX, s.BeforeY, s.BeforeHeading, s.EnergyBeforeDrain, s.PostDrainEnergy,
            i?.Input0, i?.Input1, i?.Input2, i?.Input3, i?.Input4, i?.Input5, o?.Output0, o?.Output1,
            s.TargetKind, s.TargetId, s.TargetX, s.TargetY, distance, s.AppliedRotation, s.EffectiveSpeed, s.CommandedHeading,
            s.MovedX, s.MovedY, s.MovedHeading, s.ReflectedX, s.ReflectedY, displacement, row.PathTurn,
            s.CommittedX, s.CommittedY, s.CommittedHeading, s.EnergyAfterTick, s.FoodPickedUp, s.FoodId, s.FoodEnergy,
            s.BirthsThisTick, row.Population, row.Herbivores, row.FoodCount, row.PredationCount, replay.GenomeDigest];
        return string.Join(',', fields.Select(Value));
    }
    internal static void WriteBounded(TextWriter writer, string header, IEnumerable<string> lines)
    {
        long bytes = 3;
        foreach (var line in new[] { header }.Concat(lines))
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(line) + 2);
            if (bytes > ArtifactLimitBytes) throw new IOException("Research CSV exceeded 50 MB; publication aborted.");
            writer.Write(line); writer.Write("\r\n");
        }
    }
}
