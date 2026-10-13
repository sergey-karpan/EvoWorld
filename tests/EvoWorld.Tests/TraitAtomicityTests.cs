using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitAtomicityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MutationAndConstructionFailuresPreserveTransactionAndRetry(int stage)
    {
        Simulation Create() => TraitFixture.Create([new Organism(0, 50, 40, 0, 160, 0, 0)], settings: TraitFixture.Settings(foodCap: 2));
        var simulation = Create(); var clean = Create(); var state = simulation.State;
        var field = typeof(Simulation).GetField("_advancement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transaction = field.GetValue(simulation); var random = simulation.RandomState;
        var organismId = simulation.NextOrganismId; var foodId = simulation.NextFoodId;
        simulation.FailBeforeMutationForTesting = stage == 0;
        simulation.FailOffspringTraitConstructionForTesting = stage == 1;
        simulation.FailWorldConstructionForTesting = stage == 2;
        if (stage == 0) Assert.Throws<InvalidOperationException>(simulation.AdvanceTick);
        else if (stage == 1) Assert.Equal("speed", Assert.Throws<ArgumentOutOfRangeException>(simulation.AdvanceTick).ParamName);
        else Assert.Equal("organisms", Assert.Throws<ArgumentNullException>(simulation.AdvanceTick).ParamName);
        Assert.Same(transaction, field.GetValue(simulation)); Assert.Same(state, simulation.State);
        Assert.Same(state.World.Organisms, simulation.State.World.Organisms); Assert.Same(state.World.FoodItems, simulation.State.World.FoodItems);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(organismId, simulation.NextOrganismId); Assert.Equal(foodId, simulation.NextFoodId);
        simulation.FailBeforeMutationForTesting = false; simulation.FailOffspringTraitConstructionForTesting = false; simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        Assert.Equal(clean.RandomState, simulation.RandomState); Assert.Equal(clean.NextOrganismId, simulation.NextOrganismId); Assert.Equal(clean.NextFoodId, simulation.NextFoodId);
        Assert.Equal(clean.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism)),
            simulation.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism)));
        Assert.Equal(clean.TickCount, simulation.TickCount);
        Assert.Equal(clean.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)), simulation.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
    }
}
