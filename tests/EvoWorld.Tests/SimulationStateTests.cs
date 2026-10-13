using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SimulationStateTests
{
    [Fact]
    public void StateRejectsNullWorldAndNegativeTickCount()
    {
        Assert.Equal("world", Assert.Throws<ArgumentNullException>(() => new SimulationState(null!, 0)).ParamName);
        var world = WorldInitializer.Create(new SimulationSettings(42UL, 1, 10, 10, 0, 0));
        Assert.Equal("tickCount", Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationState(world, -1)).ParamName);
        Assert.Same(world, new SimulationState(world, long.MaxValue).World);
    }

    [Fact]
    public void AdvancingReplacesOneStateWithoutChangingPreviousSnapshotsOrFood()
    {
        var simulation = MovementTests.Make(5, 5, 0, 1, 1);
        var previous = simulation.State;
        simulation.AdvanceTick();
        Assert.NotSame(previous, simulation.State);
        Assert.NotSame(previous.World, simulation.State.World);
        Assert.Equal(0, previous.TickCount);
        Assert.Equal(5.0, previous.World.Organisms[0].X);
        Assert.Equal(6.0, simulation.State.World.Organisms[0].X);
        Assert.NotSame(previous.World.Organisms[0], simulation.State.World.Organisms[0]);
        Assert.Same(previous.World.FoodItems[0], simulation.State.World.FoodItems[0]);
    }

    [Fact]
    public void TickOverflowLeavesStateAndRandomUnchanged()
    {
        var settings = new SimulationSettings(42UL, 1, 10, 10, 1, 1, 0, 1, 8, 2, 0, 150, 50, 50, 0);
        var simulation = new Simulation(settings, WorldInitializer.Create(settings), long.MaxValue);
        var before = simulation.State;
        var random = simulation.RandomState;
        Assert.Throws<OverflowException>(simulation.AdvanceTick);
        Assert.Same(before, simulation.State);
        Assert.Equal(long.MaxValue, simulation.TickCount);
        Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void InternalSeamRejectsInvalidStartingStates()
    {
        var settings = new SimulationSettings(42UL, 1, 10, 10, 1, 0);
        var valid = WorldInitializer.Create(settings);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Simulation(settings, valid, -1));
        Assert.Throws<ArgumentException>(() => new Simulation(settings, new WorldState(20, 10, valid.Organisms, [])));
        var invalid = new WorldState(10, 10, [new Organism(0, 10, 0, 0, 100)], []);
        Assert.Throws<ArgumentException>(() => new Simulation(settings, invalid));
    }
}
