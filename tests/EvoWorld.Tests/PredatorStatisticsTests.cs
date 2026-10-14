using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorStatisticsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    [InlineData(2, 3)]
    public void FreshSpeciesCountsCoverEntirePopulationAndStatisticsReadsHaveNoEffects(int herbivores, int predators)
    {
        var simulation = new Simulation(PredatorFixture.Settings(herbivores, predators));
        var state = simulation.State; var random = simulation.RandomState;
        var statistics = simulation.GetStatistics(out var snapshot);
        Assert.Same(state, snapshot);
        Assert.Equal(herbivores, statistics.HerbivoreCount); Assert.Equal(predators, statistics.PredatorCount);
        Assert.Equal(statistics.OrganismCount, (long)statistics.HerbivoreCount + statistics.PredatorCount);
        Assert.Equal(0, statistics.TotalBirths); Assert.Equal(0, statistics.TotalDeaths);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
        Assert.All(StatisticsFixture.Traits(statistics), trait => Assert.Equal(herbivores + predators, trait.Count));
        if (herbivores + predators == 0) Assert.All(StatisticsFixture.Traits(statistics), trait => Assert.Equal(default, trait));
    }

    [Fact]
    public void TraitsAggregateAcrossSpeciesRatherThanCreatingSeparateMoments()
    {
        var simulation = PredatorFixture.Create(PredatorFixture.Settings(food: 0),
            PredatorFixture.Organism(Species.Herbivore, id: 7, speed: 10, metabolism: 1),
            PredatorFixture.Organism(Species.Predator, id: 43, speed: 30, metabolism: 3));
        var statistics = simulation.GetStatistics();
        Assert.Equal(20, statistics.Speed.Mean); Assert.Equal(10, statistics.Speed.StandardDeviation);
        Assert.Equal(2, statistics.Metabolism.Mean); Assert.Equal(1, statistics.Metabolism.StandardDeviation);
        Assert.Equal(1, statistics.HerbivoreCount); Assert.Equal(1, statistics.PredatorCount);
    }

    [Theory]
    [InlineData(Species.Herbivore)]
    [InlineData(Species.Predator)]
    public void DeathsAndBirthsUseCombinedHistoryAndCurrentSpeciesDistribution(Species dying)
    {
        var other = dying == Species.Herbivore ? Species.Predator : Species.Herbivore;
        var simulation = PredatorFixture.Create(PredatorFixture.Settings(food: 0, foodCap: 0, duration: 1),
            PredatorFixture.Organism(dying, id: 7, energy: 0.5, metabolism: 1), PredatorFixture.Organism(other, id: 43));
        simulation.AdvanceTick(); var statistics = simulation.GetStatistics();
        Assert.Equal(2, statistics.OrganismCount); Assert.Equal(1, statistics.TotalBirths); Assert.Equal(1, statistics.TotalDeaths);
        Assert.Equal(other == Species.Herbivore ? 2 : 0, statistics.HerbivoreCount);
        Assert.Equal(other == Species.Predator ? 2 : 0, statistics.PredatorCount);
    }

    [Fact]
    public void BothSpeciesExtinctionProducesZeroCountsAndOldEmptyStatistics()
    {
        var simulation = PredatorFixture.Create(PredatorFixture.Settings(food: 0, foodCap: 0, duration: 1),
            PredatorFixture.Organism(Species.Herbivore, id: 7, energy: 0.5, metabolism: 1),
            PredatorFixture.Organism(Species.Predator, id: 43, energy: 0.5, metabolism: 1));
        simulation.AdvanceTick(); var statistics = simulation.GetStatistics();
        Assert.Equal(0, statistics.OrganismCount); Assert.Equal(0, statistics.HerbivoreCount); Assert.Equal(0, statistics.PredatorCount);
        Assert.Equal(2, statistics.TotalDeaths); Assert.Equal(0, statistics.TotalBirths);
        Assert.All(StatisticsFixture.Traits(statistics), t => Assert.Equal(default, t));
    }

    [Fact]
    public void SpeciesPassPreservesAllocationFreeWarmedStatistics()
    {
        var simulation = new Simulation(PredatorFixture.Settings(herbivores: 5, predators: 5));
        for (var i = 0; i < 100; i++) simulation.GetStatistics();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) simulation.GetStatistics();
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
