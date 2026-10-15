using System.Globalization;
using System.Text;
using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

internal sealed record EnergyCase(long ContextTick, string Stratum, double ObservedEnergy, ResearchInputs Inputs,
    double Energy, ResearchOutputs Outputs, double Rotation, double EffectiveSpeed);
internal readonly record struct Sensitivity(double MaxAbsDelta, double MinSlope, double MaxSlope, int SignChanges);

internal static class FrozenAnalysis
{
    internal const string CounterfactualHeader = "ContextTick,Stratum,ObservedPostDrainEnergy,Energy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,PredictedRotation,PredictedEffectiveSpeed,GenomeSha256";
    internal const string SensitivityHeader = "ContextTick,Stratum,ObservedPostDrainEnergy,Output,MaxAbsDeltaOutput,MinDeltaOutputPerUnitEnergy,MaxDeltaOutputPerUnitEnergy,SignChangeCount";
    internal const string ContrastHeader = "BaseTick,DonorTick,ChangedChannels,FixedInput3,BaseOutput0,ContrastOutput0,DeltaOutput0,BaseOutput1,ContrastOutput1,DeltaOutput1";
    internal const string TransitionHeader = "BaseTick,TransitionTick,Intervention,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,DeltaOutput0FromBase,ActualTransitionOutput0";

    internal static IReadOnlyList<(string Stratum, OrganismResearchSample Sample)> Contexts(ReplayResult replay)
    {
        var eligible = replay.Rows.Where(r => r.Sample.Inputs is not null).Select(r => r.Sample).ToArray();
        var first = eligible[0].TickCount; var span = eligible[^1].TickCount - first + 1;
        var result = new List<(string, OrganismResearchSample)>(); var used = new HashSet<long>();
        // Temporal thirds are coverage strata, not asserted loop/escape phase labels.
        for (var third = 0; third < 3; third++)
        {
            var population = eligible.Where(s => Math.Min(2, (s.TickCount - first) * 3 / span) == third).ToArray();
            foreach (var energy in new[] { 100.0, 50.0, 20.0 })
            {
                var sample = population.Where(s => !used.Contains(s.TickCount)).OrderBy(s => Math.Abs(s.PostDrainEnergy!.Value - energy)).ThenBy(s => s.TickCount).FirstOrDefault();
                if (sample is null) continue;
                used.Add(sample.TickCount); result.Add(($"TemporalThird{third + 1}", sample));
            }
        }
        // Additional objective event anchors, not a presumed energy threshold or phase fit.
        foreach (var (label, sample) in new[]
        {
            ("FirstEnergyAtMost30", eligible.FirstOrDefault(s => s.PostDrainEnergy <= 30)),
            ("FirstPositiveSteering", eligible.FirstOrDefault(s => s.Outputs!.Value.Output0 > 0)),
            ("FirstReflection", eligible.FirstOrDefault(s => s.ReflectedX == true || s.ReflectedY == true))
        })
            if (sample is not null && used.Add(sample.TickCount)) result.Add((label, sample));
        return result.AsReadOnly();
    }

    internal static EnergyCase[] Evaluate(NeuralGenome brain, OrganismResearchSample context, string stratum,
        IEnumerable<double> energies, SimulationSettings settings)
    {
        var input = new double[6]; var output = new double[2]; var encoded = new double[6];
        context.Inputs!.Value.CopyTo(input);
        // Use the production encoder for channel 3; its other channels are discarded.
        var encodingOrganism = new Organism(36, 0, 0, 0, 100, context.InheritedSpeed!.Value, 0, 0, context.SteeringRate!.Value, brain);
        var limit = settings.TickDurationSeconds > 1 && context.SteeringRate.Value > Math.PI / settings.TickDurationSeconds
            ? Math.PI : Math.Min(context.SteeringRate.Value * settings.TickDurationSeconds, Math.PI);
        var result = new List<EnergyCase>();
        foreach (var energy in energies)
        {
            NeuralControl.EncodeInputs(encodingOrganism, energy, default(SensedTarget), settings, encoded);
            input[3] = encoded[3];
            brain.Forward(input, output); // Exactly one offline forward call per energy case, buffers reused.
            result.Add(new(context.TickCount, stratum, context.PostDrainEnergy!.Value,
                new(input[0], input[1], input[2], input[3], input[4], input[5]), energy, new(output[0], output[1]),
                output[0] * limit, context.InheritedSpeed.Value * (1 + settings.SpeedModulationAmplitude * output[1])));
        }
        return result.ToArray();
    }

    internal static Sensitivity Measure(EnergyCase[] cases, bool speed)
    {
        var ordered = cases.OrderBy(c => c.Energy).ToArray();
        double Output(EnergyCase c) => speed ? c.Outputs.Output1 : c.Outputs.Output0;
        var slopes = ordered.Zip(ordered.Skip(1), (a, b) => (Output(b) - Output(a)) / (b.Energy - a.Energy)).ToArray();
        var signs = ordered.Select(c => Math.Sign(Output(c))).Where(s => s != 0).ToArray();
        return new(ordered.Max(Output) - ordered.Min(Output), slopes.Min(), slopes.Max(), signs.Zip(signs.Skip(1), (a, b) => a != b ? 1 : 0).Sum());
    }

    internal static string CaseLine(EnergyCase c, string digest) => string.Join(',', new object?[]
    {
        c.ContextTick, c.Stratum, c.ObservedEnergy, c.Energy, c.Inputs.Input0, c.Inputs.Input1, c.Inputs.Input2,
        c.Inputs.Input3, c.Inputs.Input4, c.Inputs.Input5, c.Outputs.Output0, c.Outputs.Output1, c.Rotation, c.EffectiveSpeed, digest
    }.Select(EnergyResearch.Value));

    internal static void Export(string directory)
    {
        var replay = EnergyResearch.Replay.Value; var analysisWatch = System.Diagnostics.Stopwatch.StartNew();
        var rangeText = Environment.GetEnvironmentVariable("EVOWORLD_RESEARCH_TICK_RANGE");
        var range = TraceRange(rangeText, replay.BirthTick, replay.EndTick);
        var traceRows = replay.Rows.Where(r => r.Sample.TickCount >= range.Start && r.Sample.TickCount <= range.End).ToArray();
        if (traceRows.Length == 0) throw new ArgumentOutOfRangeException(nameof(rangeText), "Requested range contains no observed #36 samples.");
        var brain = new NeuralGenome(replay.Weights); var contexts = Contexts(replay);
        var cases = contexts.Select(c => Evaluate(brain, c.Sample, c.Stratum,
            new[] { 0.0, 10, 20, 30, 40, 50, 70, 100, 130, c.Sample.PostDrainEnergy!.Value }.Distinct().Order(), replay.Settings)).ToArray();
        var sensitivity = new List<string>();
        for (var index = 0; index < cases.Length; index++)
            foreach (var speed in new[] { false, true })
            {
                var m = Measure(cases[index], speed); var c = contexts[index];
                sensitivity.Add(string.Join(',', new object?[] { c.Sample.TickCount, c.Stratum, c.Sample.PostDrainEnergy, speed ? "Output1" : "Output0",
                    m.MaxAbsDelta, m.MinSlope, m.MaxSlope, m.SignChanges }.Select(EnergyResearch.Value)));
            }
        var contrasts = new List<string>();
        foreach (var c in contexts)
        {
            var donor = contexts.OrderByDescending(d => Math.Abs(d.Sample.Inputs!.Value.Input0 - c.Sample.Inputs!.Value.Input0)
                + Math.Abs(d.Sample.Inputs.Value.Input1 - c.Sample.Inputs.Value.Input1) + Math.Abs(d.Sample.Inputs.Value.Input2 - c.Sample.Inputs.Value.Input2)).First();
            foreach (var wall in new[] { false, true })
            {
                var input = new double[6]; var baseline = new double[2]; var altered = new double[2]; c.Sample.Inputs!.Value.CopyTo(input);
                brain.Forward(input, baseline);
                var other = donor.Sample.Inputs!.Value;
                if (wall) input[5] = other.Input5;
                else { input[0] = other.Input0; input[1] = other.Input1; input[2] = other.Input2; }
                brain.Forward(input, altered);
                contrasts.Add(string.Join(',', new object?[] { c.Sample.TickCount, donor.Sample.TickCount, wall ? "5" : "0-2", input[3],
                    baseline[0], altered[0], altered[0] - baseline[0], baseline[1], altered[1], altered[1] - baseline[1] }.Select(EnergyResearch.Value)));
            }
        }
        var transitionLines = TransitionContrasts(replay, brain, contexts);
        if (replay.ReplaySeconds + analysisWatch.Elapsed.TotalSeconds >= 1800) throw new TimeoutException("Full probe budget exceeded.");
        // Publish only after both complete replays and all offline calculations succeeded.
        Directory.CreateDirectory(directory);
        void Publish(string file, string header, IEnumerable<string> lines) => PopulationHistoryCsv.ExportAtomic(Path.Combine(directory, file), lines,
            (writer, rows) => EnergyResearch.WriteBounded(writer, header, rows));
        Publish("organism36-trace.csv", EnergyResearch.TraceHeader, traceRows.Select(r => EnergyResearch.TraceLine(replay, r)));
        Publish("energy-counterfactual.csv", CounterfactualHeader, cases.SelectMany(c => c).Select(c => CaseLine(c, replay.GenomeDigest)));
        Publish("sensitivity.csv", SensitivityHeader, sensitivity);
        Publish("alternative-channel-contrasts.csv", ContrastHeader, contrasts);
        Publish("transition-contrasts.csv", TransitionHeader, transitionLines);
        Publish("genome36.csv", "ParameterIndex,Float64BitsHex,RoundTripValue", replay.Weights.Select((w, i) =>
            $"{i},{unchecked((ulong)BitConverter.DoubleToInt64Bits(w)):X16},{EnergyResearch.Value(w)}"));
        var metadata = new List<string>
        {
            "Key,Value", "BirthTick," + replay.BirthTick, "LastAliveTick," + replay.LastAliveTick, "EndTick," + replay.EndTick,
            "FinalPredationCount," + replay.FinalPredationCount, "ComparedSnapshots," + replay.ComparedSnapshots,
            "ReplaySeconds," + EnergyResearch.Value(replay.ReplaySeconds), "AnalysisSeconds," + EnergyResearch.Value(analysisWatch.Elapsed.TotalSeconds),
            "GenomeSha256," + replay.GenomeDigest, "Runtime," + Environment.Version,
            "CoreInformationalVersion," + typeof(Simulation).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            string.IsNullOrEmpty(rangeText) ? "TraceCoverage,Full lifespan including newborn and disappearance ticks; no omitted intervals"
                : "TraceCoverage,Explicit manual range only; outside-range trace rows omitted; full replay and counterfactual context selection still run",
            "FirstExportedTick," + traceRows[0].Sample.TickCount, "LastExportedTick," + traceRows[^1].Sample.TickCount,
            "SourceStatus,Authorized uncommitted Task12c-mini2 diagnostic seam; public APIs and mechanics unchanged",
            "ReferenceObservation,None; public and research diagnostics disabled",
            "ContextSelection,Three nearest distinct energies to 100/50/20 in each temporal third plus first energy<=30/positive steering/reflection; not inferred behavioral phases"
        };
        metadata.AddRange(typeof(SimulationSettings).GetProperties().Select(p => p.Name + "," + EnergyResearch.Value(p.GetValue(replay.Settings))));
        Publish("run-metadata.csv", metadata[0], metadata.Skip(1));
        if (replay.ReplaySeconds + analysisWatch.Elapsed.TotalSeconds >= 1800) throw new TimeoutException("Full probe including publication exceeded its budget.");
    }

    internal static (long Start, long End) TraceRange(string? text, long birth, long end)
    {
        if (string.IsNullOrEmpty(text)) return (birth, end);
        var parts = text.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var finish)
            || start > finish || finish > end) throw new ArgumentException("Trace range must be inclusive nonnegative start:end within the replay.", nameof(text));
        return (start, finish);
    }

    internal static List<string> TransitionContrasts(ReplayResult replay, NeuralGenome brain,
        IReadOnlyList<(string Stratum, OrganismResearchSample Sample)> contexts)
    {
        var target = contexts.FirstOrDefault(c => c.Stratum == "FirstPositiveSteering").Sample;
        if (target is null) return [];
        var basis = contexts.Where(c => c.Sample.TickCount < target.TickCount && c.Sample.TargetKind == TargetKind.None
            && c.Sample.Inputs!.Value.Input5 == -1).OrderByDescending(c => c.Sample.TickCount).First().Sample;
        var original = new double[6]; basis.Inputs!.Value.CopyTo(original); var actual = new double[6]; target.Inputs!.Value.CopyTo(actual);
        var result = new List<string>();
        foreach (var intervention in new[] { "None", "EnergyOnly", "FoodOnly", "WallOnly", "FoodAndWall", "AllChangedInputs" })
        {
            var inputs = (double[])original.Clone(); var outputs = new double[2];
            if (intervention is "EnergyOnly" or "AllChangedInputs") inputs[3] = actual[3];
            if (intervention is "FoodOnly" or "FoodAndWall" or "AllChangedInputs") { inputs[0] = actual[0]; inputs[1] = actual[1]; inputs[2] = actual[2]; }
            if (intervention is "WallOnly" or "FoodAndWall" or "AllChangedInputs") inputs[5] = actual[5];
            brain.Forward(inputs, outputs);
            result.Add(string.Join(',', new object?[] { basis.TickCount, target.TickCount, intervention,
                inputs[0], inputs[1], inputs[2], inputs[3], inputs[4], inputs[5], outputs[0], outputs[1],
                outputs[0] - basis.Outputs!.Value.Output0, target.Outputs!.Value.Output0 }.Select(EnergyResearch.Value)));
        }
        return result;
    }
}

public sealed class FrozenGenomeCounterfactualTests
{
    [Fact]
    public void MeasuredContextsAndFrozenGenomePreserveFiveChannelBitsAndActualOutputs()
    {
        var replay = EnergyResearch.Replay.Value; var contexts = FrozenAnalysis.Contexts(replay);
        Assert.True(contexts.Count >= 9); var brain = new NeuralGenome(replay.Weights); var digest = EnergyResearch.Digest(replay.Weights);
        foreach (var (_, sample) in contexts)
        {
            var cases = FrozenAnalysis.Evaluate(brain, sample, "Measured", new[] { 0.0, 10, 20, 30, 40, 50, 70, 100, 130, sample.PostDrainEnergy!.Value }.Distinct(), replay.Settings);
            foreach (var c in cases)
            {
                double[] expected = new double[6], actual = new double[6]; sample.Inputs!.Value.CopyTo(expected); c.Inputs.CopyTo(actual);
                foreach (var channel in new[] { 0, 1, 2, 4, 5 }) Assert.Equal(BitConverter.DoubleToInt64Bits(expected[channel]), BitConverter.DoubleToInt64Bits(actual[channel]));
                Assert.True(double.IsFinite(c.Outputs.Output0)); Assert.True(double.IsFinite(c.Outputs.Output1));
            }
            Assert.Equal(cases.Single(c => c.Energy == 100).Outputs, cases.Single(c => c.Energy == 130).Outputs);
            Assert.Equal(sample.Outputs, cases.Single(c => c.Energy == sample.PostDrainEnergy.Value).Outputs);
        }
        Assert.Equal(digest, EnergyResearch.Digest(brain.Weights.ToArray()));
    }

    [Fact]
    public void ResearchSchemasFormattingAndUnavailableSignalsAreExplicit()
    {
        Assert.Equal("Seed,OrganismId,ParentId,Tick,Status,DidAct,AliveAfterTick,BeforeX,BeforeY,BeforeHeading,EnergyBeforeDrain,PostDrainEnergy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,TargetKind,TargetId,TargetX,TargetY,TargetDistance,AppliedRotation,EffectiveSpeed,CommandedHeading,MovedX,MovedY,MovedHeading,ReflectedX,ReflectedY,Displacement,PathTurn,CommittedX,CommittedY,CommittedHeading,EnergyAfterTick,FoodPickedUp,FoodId,FoodEnergy,BirthsThisTick,Population,Herbivores,FoodCount,PredationCount,GenomeSha256", EnergyResearch.TraceHeader);
        Assert.Equal("ContextTick,Stratum,ObservedPostDrainEnergy,Energy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,PredictedRotation,PredictedEffectiveSpeed,GenomeSha256", FrozenAnalysis.CounterfactualHeader);
        var prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA"); Assert.Equal("0.1", EnergyResearch.Value(0.1)); Assert.Equal("18446744073709551615", EnergyResearch.Value(ulong.MaxValue)); }
        finally { CultureInfo.CurrentCulture = prior; }
        var replay = EnergyResearch.Replay.Value; var lines = replay.Rows.Select(r => EnergyResearch.TraceLine(replay, r)).ToArray();
        Assert.All(lines, line => Assert.Equal(48, line.Split(',').Length));
        var columns = EnergyResearch.TraceHeader.Split(','); var newborn = lines[0].Split(',');
        Assert.Equal("", newborn[Array.IndexOf(columns, "Input3")]); Assert.Equal("", newborn[Array.IndexOf(columns, "FoodPickedUp")]);
        Assert.Equal(Enumerable.Range(checked((int)replay.BirthTick), replay.Rows.Count).Select(t => (long)t), replay.Rows.Select(r => r.Sample.TickCount));
    }

    [Fact]
    public void BoundedSafePublicationPreservesPreviousTargetOnFailure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoWorld-ResearchCsv-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "trace.csv"); File.WriteAllText(target, "previous");
            Assert.Throws<IOException>(() => PopulationHistoryCsv.ExportAtomic(target, 0, (writer, _) =>
            {
                writer.Write("partial"); throw new IOException("Injected serialization failure");
            }));
            Assert.Equal("previous", File.ReadAllText(target)); Assert.Single(Directory.GetFiles(directory));
            var largeLine = new string('x', 1_000_000);
            Assert.Throws<IOException>(() => EnergyResearch.WriteBounded(TextWriter.Null, "Header", Enumerable.Repeat(largeLine, 51)));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void OfflineEnergyCasesPreserveReplayStateRngInputsAndCapturedSample()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 36, energy: 75, steering: 2, speed: 10)], neural: true);
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true; simulation.AdvanceTick();
        var state = simulation.State; var random = simulation.RandomState; var sample = simulation.LastResearchSample!;
        var genome = state.World.Organisms.Single().Brain; var digest = EnergyResearch.Digest(genome.Weights.ToArray());
        var cases = FrozenAnalysis.Evaluate(genome, sample, "Test", new[] { 0.0, 10, 100, 130 }, simulation.Settings);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Same(sample, simulation.LastResearchSample);
        Assert.Equal(digest, EnergyResearch.Digest(genome.Weights.ToArray()));
        Assert.Equal(-1, cases[0].Inputs.Input3); Assert.Equal(1, cases[2].Inputs.Input3); Assert.Equal(cases[2].Outputs, cases[3].Outputs);
    }

    [Fact]
    public void SensitivityUsesPairwiseRangeSignedSlopesAndZeroExcludedSignChanges()
    {
        var cases = new[] { -1.0, 0, 1, -1 }.Select((output, energy) => new EnergyCase(0, "Synthetic", 0, default,
            energy, new(output, energy == 0 ? -0.5 : 0.5), 0, 0)).ToArray();
        Assert.Equal(new Sensitivity(2, -2, 1, 2), FrozenAnalysis.Measure(cases, false));
        Assert.Equal(new Sensitivity(1, 0, 1, 1), FrozenAnalysis.Measure(cases, true));
        Assert.Equal("ContextTick,Stratum,ObservedPostDrainEnergy,Output,MaxAbsDeltaOutput,MinDeltaOutputPerUnitEnergy,MaxDeltaOutputPerUnitEnergy,SignChangeCount", FrozenAnalysis.SensitivityHeader);
        Assert.Equal("BaseTick,DonorTick,ChangedChannels,FixedInput3,BaseOutput0,ContrastOutput0,DeltaOutput0,BaseOutput1,ContrastOutput1,DeltaOutput1", FrozenAnalysis.ContrastHeader);
    }

    [Fact]
    public void ResearchFileCreationAndReplacementPreserveExactBomCrLfAndInvariantPayload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoWorld-ResearchBytes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            var replay = EnergyResearch.Replay.Value;
            var snapshot = replay.Rows.Take(3).Select(r => EnergyResearch.TraceLine(replay, r)).ToArray();
            var target = Path.Combine(directory, "trace.csv");
            for (var count = 1; count <= 3; count++)
            {
                PopulationHistoryCsv.ExportAtomic(target, snapshot.Take(count), (writer, rows) => EnergyResearch.WriteBounded(writer, EnergyResearch.TraceHeader, rows));
                var expected = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(EnergyResearch.TraceHeader + "\r\n" + string.Join("\r\n", snapshot.Take(count)) + "\r\n")).ToArray();
                Assert.Equal(expected, File.ReadAllBytes(target)); Assert.Single(Directory.GetFiles(directory));
            }
        }
        finally { CultureInfo.CurrentCulture = prior; Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("-1:100")]
    [InlineData("300:200")]
    [InlineData("10")]
    [InlineData("a:b")]
    [InlineData("0:23631")]
    [InlineData("0:9223372036854775808")]
    public void ManualTraceRangeRejectsInvalidRequestsInsteadOfSilentlyTruncating(string text) =>
        Assert.Throws<ArgumentException>(() => FrozenAnalysis.TraceRange(text, 2027, 23630));

    [Fact]
    public void ManualTraceRangeHasExplicitInclusiveBoundsAndDefaultCoversEntireLife()
    {
        Assert.Equal((2027L, 23630L), FrozenAnalysis.TraceRange(null, 2027, 23630));
        Assert.Equal((10000L, 14000L), FrozenAnalysis.TraceRange("10000:14000", 2027, 23630));
    }

    [Fact]
    public void ExportResearchArtifactsOnlyWhenExplicitlyRequested()
    {
        var directory = Environment.GetEnvironmentVariable("EVOWORLD_RESEARCH_OUTPUT");
        if (!string.IsNullOrWhiteSpace(directory)) FrozenAnalysis.Export(Path.GetFullPath(directory));
    }
}
