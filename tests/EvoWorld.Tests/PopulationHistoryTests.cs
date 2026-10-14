using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PopulationHistoryTests
{
    internal static Simulation DistinctInactiveGenomes(long tick = 0) => new(
        PredationFixture.Settings(herbivores: 2, predators: 2, radius: 0),
        new WorldState(100, 80, new[]
        {
            new Organism(7, 10, 10, 0, 100, 1, 1, 1, 1, new NeuralGenome(Enumerable.Repeat(-8.0, 74).ToArray()), Species.Herbivore),
            new Organism(43, 20, 10, 0, 100, 3, 3, 3, 3, new NeuralGenome(Enumerable.Repeat(8.0, 74).ToArray()), Species.Herbivore),
            new Organism(99, 30, 10, 0, 100, 2, 2, 2, 2, new NeuralGenome(Enumerable.Repeat(-4.0, 74).ToArray()), Species.Predator),
            new Organism(123, 40, 10, 0, 100, 4, 4, 4, 4, new NeuralGenome(Enumerable.Repeat(4.0, 74).ToArray()), Species.Predator)
        }, []), tick);

    [Fact]
    public void RuleBasedDistinctInactiveGenomesRemainNullForBothSpeciesInEverySample()
    {
        var history = new PopulationHistory(DistinctInactiveGenomes());
        for (var tick = 100; tick <= 1000; tick += 100) Assert.True(history.Observe(DistinctInactiveGenomes(tick)));
        Assert.False(history.Metadata.NeuralGenomeActive); Assert.Equal(11, history.Count);
        Assert.All(history, sample =>
        {
            Assert.Null(sample.Herbivores.MeanParameterVariance); Assert.Null(sample.Predators.MeanParameterVariance);
            Assert.Equal(2, sample.HerbivoreCount); Assert.Equal(2, sample.PredatorCount);
            Assert.Equal(2.0, sample.Herbivores.Speed.Mean); Assert.Equal(3.0, sample.Predators.Speed.Mean);
        });
    }

    internal static Simulation Empty(long tick = 0, double duration = 1, long predations = 0) =>
        new(PredationFixture.Settings(herbivores: 0, predators: 0, duration: duration),
            new WorldState(100, 80, [], []), tick, null, null, predations);

    [Fact]
    public void TickZeroAndActuallyObservedBoundariesOnlyWithNoDuplicates()
    {
        var simulation = Empty(); var history = new PopulationHistory(simulation);
        Assert.Single(history); Assert.Equal(0, history[0].Tick); Assert.Equal(0, history[0].BirthsSincePreviousSample);
        Assert.False(history.Observe(simulation));
        for (var tick = 1; tick <= 301; tick++)
        {
            simulation.AdvanceTick(); var previous = history.Revision;
            Assert.Equal(tick % 100 == 0, history.Observe(simulation));
            Assert.Equal(previous + (tick % 100 == 0 ? 1 : 0), history.Revision);
            Assert.False(history.Observe(simulation));
        }
        Assert.Equal(new long[] { 0, 100, 200, 300 }, history.Select(s => s.Tick));
        Assert.All(history, s => Assert.Equal(0, s.MissedSamplingBoundaries));
    }

    [Theory]
    [InlineData(99, 0)]
    [InlineData(150, 1)]
    [InlineData(250, 2)]
    [InlineData(399, 3)]
    public void OffBoundarySkipsLeaveHistoryUnchangedAndMissesAppearOnlyInNextScheduledSample(long offBoundary, long missed)
    {
        var history = new PopulationHistory(Empty()); var original = history[0];
        Assert.False(history.Observe(Empty(offBoundary))); Assert.Single(history); Assert.Equal(original, history[0]);
        Assert.Equal(0, history.TotalMissedSamplingBoundaries); Assert.Equal(1, history.Revision);
        var scheduled = (offBoundary / 100 + 1) * 100;
        Assert.True(history.Observe(Empty(scheduled))); Assert.Equal(missed, history[1].MissedSamplingBoundaries);
        Assert.Equal(missed, history.TotalMissedSamplingBoundaries); Assert.Equal(new[] { 0L, scheduled }, history.Select(s => s.Tick));
    }

    [Fact]
    public void SkippedSampleDeltasCoverAllElapsedTicksAndUseCoreCumulativeEvents()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey()]);
        var history = new PopulationHistory(simulation);
        for (var i = 0; i < 250; i++) simulation.AdvanceTick();
        Assert.False(history.Observe(simulation)); Assert.Single(history);
        for (var i = 0; i < 50; i++) simulation.AdvanceTick();
        Assert.True(history.Observe(simulation)); var sample = history[1]; var stats = simulation.GetStatistics();
        Assert.Equal(300, sample.Tick); Assert.Equal(2, sample.MissedSamplingBoundaries);
        Assert.Equal(stats.TotalDeaths, sample.DeathsSincePreviousSample); Assert.Equal(1, sample.DeathsSincePreviousSample);
        Assert.Equal(1, sample.PredationEventsSincePreviousSample); Assert.Equal(1, sample.TotalPredationEvents);
        Assert.Equal(0, sample.BirthsSincePreviousSample); Assert.Equal(1, sample.PredatorCount); Assert.Equal(0, sample.HerbivoreCount);
    }

    [Fact]
    public void SamplingUsesSameSnapshotAndDoesNotAlterAnyCommittedComponent()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(sensing: 30), PredationFixture.Prey()], neural: true);
        simulation.EnableDiagnosticsFor(7); for (var i = 0; i < 100; i++) simulation.AdvanceTick();
        var state = simulation.State; var random = simulation.RandomState; var sample = simulation.LastDiagnosticSample;
        var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId; var stats = simulation.GetStatistics();
        var history = new PopulationHistory(simulation); var record = Assert.Single(history);
        Assert.Equal(stats.TickCount, record.Tick); Assert.Equal(stats.OrganismCount, record.OrganismCount);
        Assert.Equal(stats.FoodCount, record.FoodCount); Assert.Equal(stats.TotalDeaths, record.TotalDeaths);
        Assert.Equal(stats.TotalPredationEvents, record.TotalPredationEvents); Assert.Equal(stats.PredatorCount, record.Predators.Speed.Count);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.Equal(foodId, simulation.NextFoodId); Assert.Equal(organismId, simulation.NextOrganismId);
    }

    [Fact]
    public void NonScheduledAndDuplicateObservationsAllocateNothingWhenWarmed()
    {
        var scheduled = Empty(); var offBoundary = Empty(99); var history = new PopulationHistory(scheduled);
        for (var i = 0; i < 100; i++) { history.Observe(offBoundary); history.Observe(scheduled); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) { history.Observe(offBoundary); history.Observe(scheduled); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread()); Assert.Single(history);
    }

    [Fact]
    public void EmptySpeciesAndExtinctionContinueSamplingWithoutFakeEvents()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(energy: 1, metabolism: 1)]);
        var history = new PopulationHistory(simulation);
        for (var i = 0; i < 200; i++) { simulation.AdvanceTick(); history.Observe(simulation); }
        Assert.Equal(3, history.Count); Assert.Equal(1, history[1].TotalDeaths); Assert.Equal(1, history[1].DeathsSincePreviousSample);
        Assert.Equal(0, history[2].DeathsSincePreviousSample); Assert.Equal(0, history[2].OrganismCount);
        Assert.Equal(default, history[2].Herbivores.Speed); Assert.Null(history[2].Herbivores.MeanParameterVariance);
        Assert.Null(history[2].Predators.MeanParameterVariance);
    }

    [Fact]
    public void ModelTimeOverflowIsNullableAndTickRemainsExact()
    {
        var history = new PopulationHistory(Empty(duration: double.MaxValue));
        Assert.Equal(0.0, history[0].ModelTimeSeconds);
        history.Observe(Empty(100, double.MaxValue)); Assert.Equal(100, history[1].Tick); Assert.Null(history[1].ModelTimeSeconds);
        var high = long.MaxValue / 100 * 100;
        history.Observe(Empty(high, double.MaxValue)); Assert.Equal(high, history[2].Tick); Assert.Null(history[2].ModelTimeSeconds);
    }

    [Fact]
    public void DecreasingCountersAndOldScheduledTicksRejectAtomically()
    {
        var history = new PopulationHistory(Empty(predations: 5)); history.Observe(Empty(100, predations: 7));
        var before = history.Capture();
        Assert.Throws<InvalidOperationException>(() => history.Observe(Empty(200, predations: 6)));
        Assert.Throws<InvalidOperationException>(() => history.Observe(Empty()));
        Assert.Equal(before.Samples, history.Capture().Samples); Assert.Equal(2, history.Revision);
    }

    [Fact]
    public void FullFiftyThousandBoundDropsOldestDeterministicallyAndSnapshotIsFrozen()
    {
        var history = new PopulationHistory(Empty()); var first = history.Capture();
        for (var i = 1; i <= 50_002; i++) history.Observe(Empty((long)i * 100));
        Assert.Equal(50_000, history.Count); Assert.Equal(3, history.DroppedSamples);
        Assert.Equal(300, history[0].Tick); Assert.Equal(5_000_200, history[^1].Tick);
        Assert.Single(first.Samples); Assert.Equal(0, first.Samples[0].Tick); Assert.Equal(0, first.DroppedSamples);
        Assert.Equal(0, history.TotalMissedSamplingBoundaries);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[-1]); Assert.Throws<ArgumentOutOfRangeException>(() => history[history.Count]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50001)]
    public void InvalidCapacityIsRejected(int capacity) => Assert.Throws<ArgumentOutOfRangeException>(() => new PopulationHistory(Empty(), capacity));

    [Fact]
    public void FifoIntervalDeltasRemainBetweenObservedSamplesAfterTruncation()
    {
        var history = new PopulationHistory(Empty(), capacity: 2);
        history.Observe(Empty(100, predations: 2)); history.Observe(Empty(300, predations: 5));
        Assert.Equal(new long[] { 100, 300 }, history.Select(s => s.Tick));
        Assert.Equal(2, history[0].PredationEventsSincePreviousSample); Assert.Equal(3, history[1].PredationEventsSincePreviousSample);
        Assert.Equal(1, history.DroppedSamples); Assert.Equal(1, history[1].MissedSamplingBoundaries);
    }

    [Fact]
    public void RecordAndMetadataHaveNoWorldOrganismStateOrGenomeReferences()
    {
        static void Scalars(Type type)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain(property.PropertyType, new[] { typeof(Simulation), typeof(SimulationState), typeof(WorldState), typeof(Organism), typeof(NeuralGenome) });
                Assert.Null(property.SetMethod?.IsPublic == true ? property.SetMethod : null);
            }
        }
        // Value records use init-only setters; their nested types are scalar summaries.
        foreach (var type in new[] { typeof(PopulationHistorySample), typeof(SpeciesHistorySummary), typeof(HistoryTraitSummary) })
            foreach (var property in type.GetProperties())
                Assert.True(property.PropertyType.IsValueType);
        Scalars(typeof(PopulationHistory));
        foreach (var property in typeof(HistoryRunMetadata).GetProperties())
            Assert.True(property.PropertyType.IsValueType || property.PropertyType == typeof(string));
    }

    [Fact]
    public void BirthIntervalDeltasUseCommittedCoreHistoryRatherThanPopulationDifferences()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 7, energy: 200)], cap: 2);
        var history = new PopulationHistory(simulation);
        for (var i = 0; i < 200; i++) { simulation.AdvanceTick(); history.Observe(simulation); }
        Assert.Equal(1, history[1].TotalBirths); Assert.Equal(1, history[1].BirthsSincePreviousSample);
        Assert.Equal(1, history[2].TotalBirths); Assert.Equal(0, history[2].BirthsSincePreviousSample);
        Assert.Equal(0, history[1].DeathsSincePreviousSample); Assert.Equal(0, history[2].DeathsSincePreviousSample);
    }

    [Theory]
    [InlineData("DroppedSamples")]
    [InlineData("TotalMissedSamplingBoundaries")]
    [InlineData("Revision")]
    public void CheckedObserverCounterOverflowPreservesEveryPreviousHistoryRecord(string property)
    {
        var history = new PopulationHistory(Empty(), capacity: 1);
        var counter = typeof(PopulationHistory).GetProperty(property,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        counter.SetValue(history, long.MaxValue);
        var before = history.Capture();
        Assert.Throws<OverflowException>(() => history.Observe(Empty(300)));
        Assert.Equal(before.Samples, history.Capture().Samples); Assert.Single(history);
        Assert.Equal(long.MaxValue, counter.GetValue(history));
    }

    [Fact]
    public void RecoveryAfterRejectedBoundariesReportsMissesInTheNextObservedScheduledSample()
    {
        var history = new PopulationHistory(Empty(predations: 5));
        Assert.Throws<InvalidOperationException>(() => history.Observe(Empty(100, predations: 4)));
        Assert.Throws<InvalidOperationException>(() => history.Observe(Empty(200, predations: 4)));
        Assert.False(history.Observe(Empty(250, predations: 6))); Assert.Single(history);
        history.Observe(Empty(300, predations: 6));
        Assert.Equal(new long[] { 0, 300 }, history.Select(s => s.Tick));
        Assert.Equal(2, history[1].MissedSamplingBoundaries); Assert.Equal(1, history[1].PredationEventsSincePreviousSample);
        Assert.Equal(2, history.TotalMissedSamplingBoundaries);
    }
}
