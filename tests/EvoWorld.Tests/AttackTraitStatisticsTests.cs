using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class AttackTraitStatisticsTests
{
    private static Organism Organism(int id, double radius, double efficiency, Species species = Species.Herbivore) =>
        new(id, 50, 40, 0, 100, 0, 0, 0, 0, EvoWorld.Core.Organism.ZeroBrain, species, null, radius, efficiency);
    private static PopulationStatistics Statistics(params Organism[] organisms) =>
        new Simulation(PredationFixture.Settings(herbivores: organisms.Count(o => o.Species == Species.Herbivore),
            predators: organisms.Count(o => o.Species == Species.Predator)), new WorldState(100, 80, organisms, [])).GetStatistics();

    [Fact]
    public void MixedSpeciesIndependentMomentsIncludeEveryLivingOrganismWithSparseIds()
    {
        var stats = Statistics(Organism(7, 1, 0.25), Organism(99, 3, 0.5, Species.Predator), Organism(int.MaxValue, 5, 0.75));
        Assert.Equal(3, stats.AttackRadius.Count); Assert.Equal(1, stats.AttackRadius.Min); Assert.Equal(5, stats.AttackRadius.Max);
        Assert.Equal(3, stats.AttackRadius.Mean); NeuralFixture.Near(Math.Sqrt(8.0 / 3), stats.AttackRadius.StandardDeviation);
        Assert.Equal(3, stats.AttackEfficiency.Count); Assert.Equal(0.25, stats.AttackEfficiency.Min); Assert.Equal(0.75, stats.AttackEfficiency.Max);
        Assert.Equal(0.5, stats.AttackEfficiency.Mean); NeuralFixture.Near(Math.Sqrt(1.0 / 24), stats.AttackEfficiency.StandardDeviation);
        Assert.Equal(2, stats.HerbivoreCount); Assert.Equal(1, stats.PredatorCount); Assert.Equal(0, stats.TotalPredationEvents);
    }

    [Fact]
    public void EmptyPopulationAndIdenticalValuesFollowExistingZeroAndCountSemantics()
    {
        var empty = Statistics(); Assert.Equal(default, empty.AttackRadius); Assert.Equal(default, empty.AttackEfficiency);
        var stats = Statistics(Organism(7, 8, 1), Organism(43, 8, 1, Species.Predator));
        Assert.Equal(2, stats.AttackRadius.Count); Assert.Equal(8, stats.AttackRadius.Mean); Assert.Equal(0, stats.AttackRadius.StandardDeviation);
        Assert.Equal(2, stats.AttackEfficiency.Count); Assert.Equal(1, stats.AttackEfficiency.Mean); Assert.Equal(0, stats.AttackEfficiency.StandardDeviation);
    }

    [Fact]
    public void ExtremeCustomRadiusMomentsAreNumericallyStable()
    {
        var stats = Statistics(Organism(7, 0, 0), Organism(43, double.MaxValue, 1, Species.Predator));
        Assert.Equal(double.MaxValue / 2, stats.AttackRadius.Mean); Assert.Equal(double.MaxValue / 2, stats.AttackRadius.StandardDeviation);
        Assert.Equal(0.5, stats.AttackEfficiency.Mean); Assert.Equal(0.5, stats.AttackEfficiency.StandardDeviation);
        var adjacent = Statistics(Organism(7, Math.BitDecrement(double.MaxValue), 0), Organism(43, double.MaxValue, 0));
        Assert.True(double.IsFinite(adjacent.AttackRadius.Mean)); Assert.True(adjacent.AttackRadius.StandardDeviation > 0);
    }

    [Theory]
    [InlineData(9.0, 1.0)]
    [InlineData(8.0, 0.5)]
    public void AdditiveStatisticsParticipateInRecordEqualityWithoutChangingOldFields(double radius, double efficiency)
    {
        var first = Statistics(Organism(7, 8, 1)); var second = Statistics(Organism(7, radius, efficiency));
        Assert.NotEqual(first, second);
        foreach (var p in typeof(PopulationStatistics).GetProperties().Where(p => p.Name is not "AttackRadius" and not "AttackEfficiency"))
            Assert.Equal(p.GetValue(first), p.GetValue(second));
        Assert.Null(typeof(PopulationStatistics).GetProperty("AttackRadius")!.SetMethod);
        Assert.Null(typeof(PopulationStatistics).GetProperty("AttackEfficiency")!.SetMethod);
    }
}
