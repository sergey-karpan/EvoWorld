using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class PopulationStatisticsTests
{
    [Fact]
    public void FreshNormalWorldHasZeroHistoryAndMatchingCounts()
    {
        var simulation = new Simulation(SimulationSettings.CreateDefault());
        var statistics = simulation.GetStatistics();
        Assert.Equal(0, statistics.TickCount); Assert.Equal(20, statistics.OrganismCount); Assert.Equal(100, statistics.FoodCount);
        Assert.Equal(0, statistics.TotalBirths); Assert.Equal(0, statistics.TotalDeaths);
        Assert.All(StatisticsFixture.Traits(statistics), trait => Assert.Equal(20, trait.Count));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1000L)]
    [InlineData(2147483648L)]
    public void SparseInjectedIdsAndDifferentConfiguredCountDoNotImplyHistory(long? nextId)
    {
        var simulation = ReproductionFixture.Create([new Organism(11, 1, 1, 0, 100), new Organism(99, 2, 2, 0, 100)],
            initialOrganisms: 5, nextOrganismId: nextId, tick: 73);
        var statistics = simulation.GetStatistics();
        Assert.Equal(73, statistics.TickCount); Assert.Equal(2, statistics.OrganismCount);
        Assert.Equal(0, statistics.TotalBirths); Assert.Equal(0, statistics.TotalDeaths);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1000L)]
    [InlineData(2147483648L)]
    public void EmptyInjectedSnapshotDoesNotReconstructEarlierEvents(long nextId)
    {
        var simulation = ReproductionFixture.Create([], initialOrganisms: 0, nextOrganismId: nextId);
        simulation.AdvanceTick();
        var statistics = simulation.GetStatistics();
        Assert.Equal(0, statistics.TotalBirths); Assert.Equal(0, statistics.TotalDeaths); Assert.Equal(0, statistics.OrganismCount);
        Assert.All(StatisticsFixture.Traits(statistics), t => Assert.Equal(default, t));
    }

    [Fact]
    public void EmptyInjectedWorldWithConfiguredPopulationUsesActualStartingCount()
    {
        var simulation = ReproductionFixture.Create([], initialOrganisms: 8, nextOrganismId: 1000);
        Assert.Equal(0, simulation.GetStatistics().TotalDeaths);
    }

    [Fact]
    public void SparseWorldCountsOnlyBirthsAndDeathsAfterInjection()
    {
        var simulation = ReproductionFixture.Create([new Organism(11, 1, 1, 0, 200), new Organism(99, 2, 2, 0, 1)],
            drain: 1, initialOrganisms: 5, nextOrganismId: 1000, tick: 37);
        Assert.Equal(0, simulation.GetStatistics().TotalBirths); Assert.Equal(0, simulation.GetStatistics().TotalDeaths);
        simulation.AdvanceTick();
        var statistics = simulation.GetStatistics();
        Assert.Equal(38, statistics.TickCount); Assert.Equal(2, statistics.OrganismCount);
        Assert.Equal(1, statistics.TotalBirths); Assert.Equal(1, statistics.TotalDeaths);
        Assert.Equal(new[] { 11, 1000 }, simulation.State.World.Organisms.Select(o => o.Id));
    }

    [Fact]
    public void MultipleBirthsAndCapBlockedBirthsUseCommittedCounter()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 250), new Organism(1, 2, 2, 0, 250)], cap: 4);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.GetStatistics().TotalBirths);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.GetStatistics().TotalBirths);
        Assert.Equal(0, simulation.GetStatistics().TotalDeaths);
    }

    [Fact]
    public void EnergyBlockedBirthsDoNotIncreaseHistory()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 100)]);
        simulation.AdvanceTick(); Assert.Equal(0, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void ExtinctionPreservesHistoryWhileRegeneratingFood()
    {
        var simulation = ReproductionFixture.Create([new Organism(17, 1, 1, 0, 1)], drain: 1, foodCap: 3, nextOrganismId: 900);
        for (var tick = 1; tick <= 3; tick++)
        {
            simulation.AdvanceTick(); var statistics = simulation.GetStatistics();
            Assert.Equal(tick, statistics.TickCount); Assert.Equal(tick, statistics.FoodCount);
            Assert.Equal(0, statistics.OrganismCount); Assert.Equal(0, statistics.TotalBirths); Assert.Equal(1, statistics.TotalDeaths);
            Assert.All(StatisticsFixture.Traits(statistics), t => Assert.Equal(default, t));
        }
    }

    [Fact]
    public void FeedingAndRegenerationCountsDescribeCurrentFoodOnly()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 100)], [new Food(9, 1, 1, 20)], foodCap: 3);
        simulation.AdvanceTick();
        Assert.Equal(1, simulation.GetStatistics().FoodCount);
        Assert.Equal(10, simulation.State.World.FoodItems[0].Id);
    }

    [Fact]
    public void FailedTransactionDoesNotChangeObservationsAndRetryCountsOnce()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 200)], foodCap: 3);
        var previous = simulation.GetStatistics(); simulation.FailWorldConstructionForTesting = true;
        Assert.Throws<ArgumentNullException>(simulation.AdvanceTick);
        StatisticsFixture.AssertBits(previous, simulation.GetStatistics());
        simulation.FailWorldConstructionForTesting = false; simulation.AdvanceTick();
        Assert.Equal(1, simulation.GetStatistics().TotalBirths);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void ImpossibleNegativeOrOverflowingHistoryThrowsInvalidOperation(long counter)
    {
        var simulation = ReproductionFixture.Create([], nextOrganismId: 1000);
        ReplaceCounter(simulation, counter);
        Assert.Throws<InvalidOperationException>(() => simulation.GetStatistics());
    }

    [Fact]
    public void ImpossibleNegativeDeathsThrowsInvalidOperation()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 100)], nextOrganismId: 1000);
        var field = typeof(Simulation).GetField("_startingOrganismCount", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(simulation, 0);
        Assert.Throws<InvalidOperationException>(() => simulation.GetStatistics());
    }

    private static void ReplaceCounter(Simulation simulation, long counter)
    {
        var field = typeof(Simulation).GetField("_advancement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previous = field.GetValue(simulation)!;
        var corrupted = Activator.CreateInstance(previous.GetType(), simulation.State, simulation.RandomState, simulation.NextFoodId, counter);
        field.SetValue(simulation, corrupted);
    }
}
