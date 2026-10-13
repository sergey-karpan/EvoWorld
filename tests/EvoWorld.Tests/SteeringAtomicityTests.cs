using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SteeringAtomicityTests
{
    [Fact]
    public void FailureAfterEarlierFoodClaimRetainsTransactionAndRetry()
    {
        Simulation Create() => SteeringFixture.Create(
            [SteeringFixture.Organism(), SteeringFixture.Organism(x: 20, y: 20, id: 9)],
            [new Food(0,10,10,20),new Food(9,20,21,20)]);
        var simulation = Create(); var clean = Create(); var state = simulation.State;
        var field = typeof(Simulation).GetField("_advancement", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var transaction = field.GetValue(simulation); var random = simulation.RandomState;
        var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId;
        simulation.FailSteeringForOrganismIdForTesting = 9;
        Assert.Throws<InvalidOperationException>(simulation.AdvanceTick);
        Assert.Same(transaction,field.GetValue(simulation)); Assert.Same(state,simulation.State);
        Assert.Same(state.World.Organisms,simulation.State.World.Organisms); Assert.Same(state.World.FoodItems,simulation.State.World.FoodItems);
        Assert.Equal(random,simulation.RandomState); Assert.Equal(foodId,simulation.NextFoodId); Assert.Equal(organismId,simulation.NextOrganismId);
        simulation.FailSteeringForOrganismIdForTesting = null; simulation.AdvanceTick(); clean.AdvanceTick();
        Assert.Equal(clean.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.SensingRadius,o.SteeringRate)),
            simulation.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.SensingRadius,o.SteeringRate)));
        Assert.Equal(clean.RandomState,simulation.RandomState);
    }

    [Fact]
    public void MovementOverflowAfterSteeringUsesExistingAtomicFailurePolicy()
    {
        var simulation = SteeringFixture.Create([SteeringFixture.Organism(speed: double.MaxValue)], [new Food(0,10,11,20)], SteeringFixture.Settings(food:1,timestep:2));
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void WorldFailureAfterRegenerationAndSixBirthDrawsPreservesBothCounters()
    {
        Simulation Create() => SteeringFixture.Create([SteeringFixture.Organism(energy:160)], [], SteeringFixture.Settings(cap:2,foodCap:2));
        var simulation = Create(); var clean = Create(); var state = simulation.State;
        var random = simulation.RandomState; var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId;
        simulation.FailWorldConstructionForTesting = true;
        Assert.Throws<ArgumentNullException>(simulation.AdvanceTick);
        Assert.Same(state,simulation.State); Assert.Equal(random,simulation.RandomState);
        Assert.Equal(foodId,simulation.NextFoodId); Assert.Equal(organismId,simulation.NextOrganismId);
        simulation.FailWorldConstructionForTesting = false; simulation.AdvanceTick(); clean.AdvanceTick();
        Assert.Equal(clean.RandomState,simulation.RandomState); Assert.Equal(clean.NextFoodId,simulation.NextFoodId); Assert.Equal(clean.NextOrganismId,simulation.NextOrganismId);
        Assert.Equal(clean.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.Speed,o.Metabolism,o.SensingRadius,o.SteeringRate)),
            simulation.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.Speed,o.Metabolism,o.SensingRadius,o.SteeringRate)));
    }
}
