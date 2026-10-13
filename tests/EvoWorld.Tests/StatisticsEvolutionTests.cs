using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class StatisticsEvolutionTests
{
    [Fact]
    public void DefaultSeedAndSettingsReproduceCompleteStatisticsAfter5000Ticks()
    {
        var first = new Simulation(SimulationSettings.CreateDefault());
        var second = new Simulation(SimulationSettings.CreateDefault());
        for (var i = 0; i < 5000; i++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            if (i % 100 == 0) StatisticsFixture.AssertBits(first.GetStatistics(), second.GetStatistics());
        }
        Assert.Equal(5000, first.GetStatistics().TickCount);
        StatisticsFixture.AssertBits(first.GetStatistics(), second.GetStatistics());
        StatisticsFixture.AssertWorlds(first, second);
    }

    [Fact]
    public void ActiveReproductionAndMutationReproduceCompleteStatisticsAfter5000Ticks()
    {
        // Deliberately low metabolism and ample initial energy ensure births;
        // all four mutation amplitudes are active. No adaptive outcome is asserted.
        var settings = new SimulationSettings(42, 0.001, 100, 80, 1, 20, 2, 0.001, 5, 0.01, 30,
            150, 50, 50, 30, 0, 0, 0, 200, 0, 5, 1, 0.0001,
            30, 2, 0, 0, 0, 100, 0, 5, 2, 0.1);
        Simulation Create() => new(settings, new WorldState(100, 80,
            [new Organism(77, 50, 40, 0, 1000, 2, 0.001, 30, 2)],
            WorldInitializer.Create(settings).FoodItems), nextOrganismId: 1000);
        var first = Create(); var second = Create();
        first.AdvanceTick(); second.AdvanceTick();
        var child = first.State.World.Organisms[1];
        Assert.NotEqual(2, child.Speed); Assert.NotEqual(0.001, child.Metabolism);
        Assert.NotEqual(30, child.SensingRadius); Assert.NotEqual(2, child.SteeringRate);
        for (var i = 1; i < 5000; i++)
        {
            first.GetStatistics(); first.AdvanceTick(); second.AdvanceTick();
            if (i % 100 == 0) StatisticsFixture.AssertBits(first.GetStatistics(), second.GetStatistics());
        }
        var statistics = first.GetStatistics();
        Assert.True(statistics.TotalBirths > 0);
        Assert.Equal(5000, statistics.TickCount);
        StatisticsFixture.AssertBits(statistics, second.GetStatistics());
        StatisticsFixture.AssertWorlds(first, second);
    }
}
