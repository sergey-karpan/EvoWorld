using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class ReproductionAtomicityTests
{
    [Fact]
    public void WorldConstructionFailureAfterFoodAndBirthDrawsRetainsExactTransactionAndRetry()
    {
        Simulation Create() => ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160)], foodCap: 2);
        var simulation = Create(); var clean = Create(); var before = simulation.State;
        var transactionField = typeof(Simulation).GetField("_advancement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transaction = transactionField.GetValue(simulation);
        var random = simulation.RandomState; var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId;
        simulation.FailWorldConstructionForTesting = true;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.Throws<ArgumentNullException>(simulation.AdvanceTick);
            Assert.Same(transaction, transactionField.GetValue(simulation)); Assert.Same(before, simulation.State);
            Assert.Same(before.World.Organisms, simulation.State.World.Organisms); Assert.Same(before.World.FoodItems, simulation.State.World.FoodItems);
            Assert.Equal(random, simulation.RandomState); Assert.Equal(foodId, simulation.NextFoodId); Assert.Equal(organismId, simulation.NextOrganismId);
        }
        simulation.FailWorldConstructionForTesting = false; simulation.AdvanceTick(); clean.AdvanceTick();
        Assert.Equal(clean.RandomState, simulation.RandomState); Assert.Equal(clean.NextFoodId, simulation.NextFoodId);
        Assert.Equal(clean.NextOrganismId, simulation.NextOrganismId); Assert.Equal(clean.TickCount, simulation.TickCount);
        Assert.Equal(clean.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray(),
            simulation.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray());
        Assert.Equal(clean.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray(),
            simulation.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray());
    }

    [Fact]
    public void IdExhaustionAfterFoodRegenerationRollsBackTentativeFood()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160)], foodCap: 2, nextOrganismId: int.MaxValue + 1L);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void LaterParentIdFailureRollsBackEarlierBirthAndFoodDraws()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160), new Organism(9, 60, 40, 0, 160)],
            foodCap: 2, nextOrganismId: int.MaxValue);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void MovementFailurePrecedesBirthAndCannotLeakIdsOrRandomState()
    {
        var settings = new SimulationSettings(42, 2, 100, 80, 1, 0, double.MaxValue, 0, 0, 1, 2, 150, 50, 50, 10);
        SurvivalFixture.AssertRollback(new Simulation(settings, new WorldState(100, 80, [new Organism(0, 50, 40, 0, 160)], [])));
    }

    [Fact]
    public void TickOverflowCannotRegenerateOrReproduce()
    {
        SurvivalFixture.AssertRollback(ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160)], foodCap: 2, tick: long.MaxValue));
    }
}
