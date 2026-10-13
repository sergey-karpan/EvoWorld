using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class EnergyTests
{
    [Theory]
    [InlineData(2, 0.25, 0, 99.5)]
    [InlineData(2, 0.5, 0, 99)]
    [InlineData(2, 0.5, 2, 99)]
    [InlineData(0, 0.5, 0, 100)]
    public void DrainUsesConfiguredDurationIndependentlyOfSpeed(double drain, double duration, double speed, double expected)
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 1, 1, 0, 100)], drain: drain, duration: duration, speed: speed);
        simulation.AdvanceTick();
        Assert.Equal(expected, Assert.Single(simulation.State.World.Organisms).Energy);
    }

    [Fact]
    public void ReadingStateDoesNotDrainAndRepeatedTicksAccumulateExactBinaryCost()
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 1, 1, 0, 100)], drain: 2, duration: 0.25);
        var original = simulation.State;
        for (var i = 0; i < 10; i++) Assert.Equal(100, simulation.State.World.Organisms[0].Energy);
        for (var i = 1; i <= 10; i++)
        {
            simulation.AdvanceTick();
            Assert.Equal(100 - i * 0.5, simulation.State.World.Organisms[0].Energy);
        }
        Assert.Equal(100, original.World.Organisms[0].Energy);
        Assert.Equal(0, original.TickCount);
    }

    [Fact]
    public void FeedingFollowsDrainAndMayExceedInitialEnergy()
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 1, 1, 0, 100)], [new Food(0, 1, 1, 20)], drain: 1);
        simulation.AdvanceTick();
        Assert.Equal(119, Assert.Single(simulation.State.World.Organisms).Energy);
        Assert.Empty(simulation.State.World.FoodItems);
    }
}
