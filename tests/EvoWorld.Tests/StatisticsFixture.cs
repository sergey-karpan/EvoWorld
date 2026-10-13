using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class StatisticsFixture
{
    internal static Simulation Values(params double[] values)
    {
        var organisms = values.Select((v, id) => new Organism(id, 10, 10, 0, 100, v, v, v, v)).ToArray();
        return TraitFixture.Create(organisms, settings: TraitFixture.Settings(values.Length, cap: values.Length));
    }

    internal static TraitStatistics[] Traits(PopulationStatistics statistics) =>
        [statistics.Speed, statistics.Metabolism, statistics.SensingRadius, statistics.SteeringRate];

    internal static void AssertBits(PopulationStatistics expected, PopulationStatistics actual)
    {
        Assert.Equal((expected.TickCount, expected.OrganismCount, expected.FoodCount, expected.TotalBirths, expected.TotalDeaths),
            (actual.TickCount, actual.OrganismCount, actual.FoodCount, actual.TotalBirths, actual.TotalDeaths));
        var first = Traits(expected); var second = Traits(actual);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].Count, second[i].Count);
            Assert.Equal(Bits(first[i]), Bits(second[i]));
        }
    }

    private static long[] Bits(TraitStatistics value) =>
        [BitConverter.DoubleToInt64Bits(value.Min), BitConverter.DoubleToInt64Bits(value.Max),
         BitConverter.DoubleToInt64Bits(value.Mean), BitConverter.DoubleToInt64Bits(value.StandardDeviation)];

    internal static void Near(double expected, double actual, double relative = 2e-15)
    {
        Assert.True(double.IsFinite(actual));
        Assert.InRange(Math.Abs(actual / expected - 1), 0, relative);
    }

    internal static void AssertWorlds(Simulation first, Simulation second)
    {
        Assert.Equal(first.RandomState, second.RandomState);
        Assert.Equal(first.NextFoodId, second.NextFoodId); Assert.Equal(first.NextOrganismId, second.NextOrganismId);
        Assert.Equal(first.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)),
            second.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)));
        Assert.Equal(first.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)),
            second.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
    }
}
