using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class StatisticsDeterminismTests
{
    [Fact]
    public void RepeatedReadsAreBitIdenticalAndPreserveEveryCommittedComponent()
    {
        var simulation = StatisticsFixture.Values(0, double.Epsilon, double.MaxValue / 2, double.MaxValue);
        var expected = simulation.GetStatistics(); var state = simulation.State;
        var random = simulation.RandomState; var organismId = simulation.NextOrganismId; var foodId = simulation.NextFoodId;
        for (var i = 0; i < 100; i++)
        {
            StatisticsFixture.AssertBits(expected, simulation.GetStatistics(out var snapshot));
            Assert.Same(state, snapshot); Assert.Same(state, simulation.State);
            Assert.Equal(random, simulation.RandomState); Assert.Equal(organismId, simulation.NextOrganismId); Assert.Equal(foodId, simulation.NextFoodId);
        }
    }

    [Fact]
    public void ReadFrequencyDoesNotAffectSubsequentWorldsOrRandomStream()
    {
        var settings = SimulationSettings.CreateDefault();
        var observed = new Simulation(settings); var unobserved = new Simulation(settings);
        for (var tick = 1; tick <= 200; tick++)
        {
            for (var read = 0; read < tick % 7; read++) observed.GetStatistics();
            observed.AdvanceTick(); unobserved.AdvanceTick();
            StatisticsFixture.AssertWorlds(unobserved, observed);
            StatisticsFixture.AssertBits(unobserved.GetStatistics(), observed.GetStatistics());
        }
    }

    [Fact]
    public void ReturnedSnapshotAndStatisticsStayConsistentAcrossCompletedTicks()
    {
        var simulation = ReproductionFixture.Create([new Organism(11, 1, 1, 0, 400), new Organism(99, 2, 2, 0, 1)],
            nextOrganismId: 1000, initialOrganisms: 5, drain: 1, foodCap: 5);
        var initial = simulation.GetStatistics(out var initialSnapshot);
        for (var tick = 1; tick <= 120; tick++)
        {
            simulation.AdvanceTick();
            var statistics = simulation.GetStatistics(out var snapshot);
            Assert.Same(simulation.State, snapshot);
            Assert.Equal(snapshot.TickCount, statistics.TickCount);
            Assert.Equal(snapshot.World.Organisms.Count, statistics.OrganismCount);
            Assert.Equal(snapshot.World.FoodItems.Count, statistics.FoodCount);
            Assert.Equal(simulation.NextOrganismId - 1000, statistics.TotalBirths);
            Assert.Equal(2 + statistics.TotalBirths - statistics.OrganismCount, statistics.TotalDeaths);
            Assert.All(StatisticsFixture.Traits(statistics), t => Assert.Equal(statistics.OrganismCount, t.Count));
            StatisticsFixture.AssertBits(statistics, simulation.GetStatistics());
        }
        Assert.Equal(0, initial.TickCount); Assert.Equal(2, initial.OrganismCount);
        Assert.Equal(400, initialSnapshot.World.Organisms[0].Energy);
    }

    [Fact]
    public void WarmedStatisticsReadsAllocateNoTemporaryCollections()
    {
        var simulation = StatisticsFixture.Values(Enumerable.Range(0, 1000).Select(i => (double)i).ToArray());
        for (var i = 0; i < 10; i++) simulation.GetStatistics();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10; i++) simulation.GetStatistics();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
