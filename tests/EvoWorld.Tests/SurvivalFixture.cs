using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class SurvivalFixture
{
    internal static Simulation Create(Organism[] organisms, Food[]? food = null,
        double drain = 0, double radius = 0, double speed = 0, double duration = 1,
        double width = 100, double height = 100, int? initialOrganisms = null, int? initialFood = null)
    {
        food ??= [];
        var settings = new SimulationSettings(42, duration, width, height,
            initialOrganisms ?? organisms.Length, initialFood ?? food.Length, speed, drain, radius, 2, 0);
        return new Simulation(settings, new WorldState(width, height, organisms, food));
    }

    internal static void AssertRollback(Simulation simulation)
    {
        var before = simulation.State;
        var random = simulation.RandomState;
        var nextFoodId = simulation.NextFoodId;
        Assert.Throws<OverflowException>(simulation.AdvanceTick);
        Assert.Same(before, simulation.State);
        Assert.Same(before.World, simulation.State.World);
        Assert.Same(before.World.Organisms, simulation.State.World.Organisms);
        Assert.Same(before.World.FoodItems, simulation.State.World.FoodItems);
        Assert.Equal(before.TickCount, simulation.TickCount);
        Assert.Equal(random, simulation.RandomState);
        Assert.Equal(nextFoodId, simulation.NextFoodId);
    }
}
