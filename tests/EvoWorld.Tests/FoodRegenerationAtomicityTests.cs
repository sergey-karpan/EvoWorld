using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class FoodRegenerationAtomicityTests
{
    [Fact]
    public void RealWorldConstructionFailureAfterTentativeDrawsPreservesTransactionAndRetry()
    {
        var settings = FoodRegenerationTests.Settings();
        var simulation = new Simulation(settings);
        var clean = new Simulation(settings);
        var state = simulation.State;
        var random = simulation.RandomState;
        var nextId = simulation.NextFoodId;
        simulation.FailWorldConstructionForTesting = true;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Equal("organisms", Assert.Throws<ArgumentNullException>(simulation.AdvanceTick).ParamName);
            Assert.Same(state, simulation.State);
            Assert.Same(state.World, simulation.State.World);
            Assert.Same(state.World.FoodItems, simulation.State.World.FoodItems);
            Assert.Same(state.World.Organisms, simulation.State.World.Organisms);
            Assert.Equal(0, simulation.TickCount);
            Assert.Equal(random, simulation.RandomState);
            Assert.Equal(nextId, simulation.NextFoodId);
        }
        simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        Assert.Equal(clean.RandomState, simulation.RandomState);
        Assert.Equal(clean.NextFoodId, simulation.NextFoodId);
        Assert.Equal(clean.TickCount, simulation.TickCount);
        Assert.Equal(clean.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray(),
            simulation.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray());
        Assert.Empty(state.World.FoodItems);
    }

    [Fact]
    public void PostDrawFailureAlsoRollsBackTentativeConsumptionDeathAndEnergy()
    {
        var settings = FoodRegenerationTests.Settings(organisms: 3, food: 1, cap: 1, drain: 1, radius: 1000);
        var world = new WorldState(100, 80,
            [new Organism(0, 1, 1, 0, 1, 0, 1), new Organism(2, 1, 1, 0, 10, 0, 1), new Organism(9, 2, 2, 0, 10, 0, 1)], [new Food(4, 1, 1, 20)]);
        var simulation = new Simulation(settings, world) { FailWorldConstructionForTesting = true };
        var previous = simulation.State;
        var random = simulation.RandomState;
        var nextId = simulation.NextFoodId;
        Assert.Throws<ArgumentNullException>(simulation.AdvanceTick);
        Assert.Same(previous, simulation.State);
        Assert.Equal(random, simulation.RandomState);
        Assert.Equal(nextId, simulation.NextFoodId);
        simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick();
        Assert.Equal(new[] { 2, 9 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(new[] { 29.0, 9.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        Assert.Equal(5, Assert.Single(simulation.State.World.FoodItems).Id);
        Assert.Equal(3, previous.World.Organisms.Count);
        Assert.Equal(4, Assert.Single(previous.World.FoodItems).Id);
    }

    [Fact]
    public void SurvivalFailureBeforeSpawnPreservesRandomAndNextId()
    {
        var settings = FoodRegenerationTests.Settings(duration: 2, drain: double.MaxValue, organisms: 1);
        SurvivalFixture.AssertRollback(new Simulation(settings));
    }

    [Fact]
    public void TickOverflowCannotDrawOrAllocateFood()
    {
        var settings = FoodRegenerationTests.Settings();
        var simulation = new Simulation(settings, new WorldState(100, 80, [], []), long.MaxValue);
        SurvivalFixture.AssertRollback(simulation);
    }
}
