using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class FoodIdContinuityTests
{
    [Fact]
    public void SequentialSpawnsFollowInitialAllocationWithoutReusingConsumedIds()
    {
        var settings = FoodRegenerationTests.Settings(food: 3, organisms: 1, radius: 1000, cap: 3);
        var world = new WorldState(100, 80, [new Organism(0, 1, 1, 0, 100)],
            [new Food(0, 1, 1, 20), new Food(2, 2, 1, 20)]);
        var simulation = new Simulation(settings, world);
        Assert.Equal(3, simulation.NextFoodId);
        for (var tick = 1; tick <= 5; tick++)
        {
            simulation.AdvanceTick();
            Assert.Equal(2, simulation.State.World.FoodItems.Count);
            Assert.Equal(2 + tick, simulation.State.World.FoodItems.Last().Id);
            Assert.Equal(3 + tick, simulation.NextFoodId);
            Assert.True(simulation.State.World.FoodItems.Zip(simulation.State.World.FoodItems.Skip(1)).All(p => p.First.Id < p.Second.Id));
        }
    }

    [Fact]
    public void HighExistingIdsDetermineNextAllocationRatherThanCollectionCount()
    {
        var settings = FoodRegenerationTests.Settings(food: 1);
        var simulation = new Simulation(settings, new WorldState(100, 80, [], [new Food(1000, 1, 1, 20)]));
        Assert.Equal(1001, simulation.NextFoodId);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 1000, 1001 }, simulation.State.World.FoodItems.Select(f => f.Id).ToArray());
    }

    [Fact]
    public void ExplicitRemovedAllocationHistoryIsPreserved()
    {
        var settings = FoodRegenerationTests.Settings(food: 5);
        var simulation = new Simulation(settings, new WorldState(100, 80, [], []), nextFoodId: 5005);
        simulation.AdvanceTick();
        Assert.Equal(5005, Assert.Single(simulation.State.World.FoodItems).Id);
        Assert.Equal(5006, simulation.NextFoodId);
    }

    [Fact]
    public void LastIntIdIsAllocatedThenExhaustionRollsBackWithoutRandomDraws()
    {
        var simulation = new Simulation(FoodRegenerationTests.Settings(cap: 2), new WorldState(100, 80, [], []), nextFoodId: int.MaxValue);
        simulation.AdvanceTick();
        Assert.Equal(int.MaxValue, Assert.Single(simulation.State.World.FoodItems).Id);
        Assert.Equal(int.MaxValue + 1L, simulation.NextFoodId);
        SurvivalFixture.AssertRollback(simulation);
        SurvivalFixture.AssertRollback(simulation); // Retrying the deterministic failure has no effects.
    }

    [Fact]
    public void ExhaustedIdsDoNotFailCapBlockedEvents()
    {
        var simulation = new Simulation(FoodRegenerationTests.Settings(cap: 1), new WorldState(100, 80, [], []), nextFoodId: int.MaxValue);
        simulation.AdvanceTick();
        var random = simulation.RandomState;
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.TickCount);
        Assert.Equal(int.MaxValue + 1L, simulation.NextFoodId);
        Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void ExhaustedIdsDoNotFailNonscheduledTicks()
    {
        var simulation = new Simulation(FoodRegenerationTests.Settings(interval: 2), new WorldState(100, 80, [], []), nextFoodId: int.MaxValue + 1L);
        simulation.AdvanceTick();
        Assert.Equal(1, simulation.TickCount);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(2L)]
    [InlineData(2147483649L)]
    public void ExplicitHistoryRejectsInvalidCounterValues(long nextId)
    {
        var settings = FoodRegenerationTests.Settings(food: 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Simulation(settings, new WorldState(100, 80, [], []), nextFoodId: nextId));
    }

    [Fact]
    public void CustomRegeneratedWorldMayContainMoreFoodThanInitialCount()
    {
        var settings = FoodRegenerationTests.Settings(cap: 2);
        var simulation = new Simulation(settings, new WorldState(100, 80, [], [new Food(4, 1, 1, 20), new Food(9, 2, 2, 20)]));
        Assert.Equal(10, simulation.NextFoodId);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 4, 9 }, simulation.State.World.FoodItems.Select(f => f.Id).ToArray());
    }
}
