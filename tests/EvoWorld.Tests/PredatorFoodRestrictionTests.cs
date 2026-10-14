using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorFoodRestrictionTests
{
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 8.0)]
    [InlineData(false, double.MaxValue)]
    [InlineData(true, 0.0)]
    [InlineData(true, 8.0)]
    [InlineData(true, double.MaxValue)]
    public void OverlappingPredatorNeverConsumesFoodRegardlessOfPickupRadius(bool neural, double radius)
    {
        var food = new Food(0, 50, 40, 20);
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(metabolism: 1)], [food], neural: neural, pickup: radius);
        var random = simulation.RandomState; simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Same(food, Assert.Single(simulation.State.World.FoodItems));
        Assert.Equal(99, simulation.State.World.Organisms[0].Energy); Assert.Equal(random, simulation.RandomState);
        Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OnlyHerbivoreConsumesSharedFoodInEitherStableIdOrder(bool predatorFirst, bool neural)
    {
        var predator = PursuitFixture.Organism(id: predatorFirst ? 7 : 43, metabolism: 1, steering: 0);
        var herbivore = PursuitFixture.Organism(Species.Herbivore, id: predatorFirst ? 43 : 7, metabolism: 1, steering: 0);
        var simulation = PursuitFixture.Create(new[] { predator, herbivore }.OrderBy(o => o.Id).ToArray(),
            [new Food(0, 50, 40, 20)], neural: neural, pickup: 0);
        simulation.EnableDiagnosticsFor(predator.Id); simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(99, simulation.State.World.Organisms.Single(o => o.Id == predator.Id).Energy);
        Assert.Equal(119, simulation.State.World.Organisms.Single(o => o.Id == herbivore.Id).Energy);
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PredatorStarvesBesidePreyAndFoodWithoutArtificialFeeding(bool neural)
    {
        var predator = PursuitFixture.Organism(x: 20, y: 20, energy: 2.5, metabolism: 1);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 21, y: 20);
        var food = new Food(0, 20, 20, 1000);
        var simulation = PursuitFixture.Create([predator, prey], [food], neural: neural, pickup: 0);
        simulation.EnableDiagnosticsFor(7);
        simulation.AdvanceTick(); Assert.Equal(1.5, simulation.State.World.Organisms[0].Energy);
        simulation.AdvanceTick(); Assert.Equal(0.5, simulation.State.World.Organisms[0].Energy);
        simulation.AdvanceTick();
        Assert.Equal(43, Assert.Single(simulation.State.World.Organisms).Id); Assert.Same(food, Assert.Single(simulation.State.World.FoodItems));
        Assert.Equal(100, simulation.State.World.Organisms[0].Energy);
        Assert.False(simulation.LastDiagnosticSample!.DidAct); Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample.TargetKind);
        Assert.Equal(1, simulation.GetStatistics().TotalDeaths);
    }
}
