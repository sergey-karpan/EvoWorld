using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class FoodPickupTests
{
    [Fact]
    public void PickupUsesPositionAfterBoundaryReflection()
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 9, 5, 0, 100)],
            [new Food(4, 8, 5, 20)], speed: 3, width: 10, height: 10);
        simulation.AdvanceTick();
        var organism = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(8, organism.X);
        Assert.Equal(Math.PI, organism.HeadingRadians);
        Assert.Equal(120, organism.Energy);
        Assert.Empty(simulation.State.World.FoodItems);
    }

    [Theory]
    [InlineData(3, 4, 5, true)]
    [InlineData(8, 0, 8, true)]
    [InlineData(0, 0, 0, true)]
    [InlineData(0.5, 0, 0, false)]
    public void PickupIncludesRadiusAndZeroRequiresCoincidence(double x, double y, double radius, bool eligible)
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)], [new Food(4, x, y, 20)], radius: radius);
        simulation.AdvanceTick();
        Assert.Equal(eligible ? 0 : 1, simulation.State.World.FoodItems.Count);
        Assert.Equal(eligible ? 120 : 100, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void FoodOneRepresentableValueBeyondRadiusIsIneligible()
    {
        var food = new Food(4, Math.BitIncrement(8.0), 0, 20);
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)], [food], radius: 8);
        simulation.AdvanceTick();
        Assert.Same(food, Assert.Single(simulation.State.World.FoodItems));
    }

    [Fact]
    public void PickupUsesPostMovementPositionAndPreservesUnconsumedFood()
    {
        var untouched = new Food(9, 20, 20, 20);
        var simulation = SurvivalFixture.Create([new Organism(0, 1, 1, 0, 100)],
            [new Food(4, 2, 1, 20), untouched], speed: 1);
        var previous = simulation.State;
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.State.World.Organisms[0].X);
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
        Assert.Same(untouched, Assert.Single(simulation.State.World.FoodItems));
        Assert.Equal(2, previous.World.FoodItems.Count);
        Assert.Equal(100, previous.World.Organisms[0].Energy);
        simulation.AdvanceTick();
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void NearestFoodOverridesCollectionOrderAndLimitIsOnePerTick()
    {
        var far = new Food(1, 4, 0, 30);
        var near = new Food(8, 1, 0, 20);
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)], [far, near], radius: 5);
        simulation.AdvanceTick();
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
        Assert.Same(far, Assert.Single(simulation.State.World.FoodItems));
        simulation.AdvanceTick();
        Assert.Equal(150, simulation.State.World.Organisms[0].Energy);
        Assert.Empty(simulation.State.World.FoodItems);
        simulation.AdvanceTick();
        Assert.Equal(150, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void ExactlyEqualDistancesChooseSmallestStableFoodId()
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 5, 5, 0, 100)],
            [new Food(2, 4, 5, 30), new Food(9, 6, 5, 20)], radius: 1);
        simulation.AdvanceTick();
        Assert.Equal(130, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(9, Assert.Single(simulation.State.World.FoodItems).Id);
    }

    [Fact]
    public void LowerOrganismIdGetsFirstOpportunityAndLaterOrganismUsesRemainingFood()
    {
        var simulation = SurvivalFixture.Create([new Organism(2, 1, 1, 0, 100), new Organism(9, 2, 1, 0, 100)],
            [new Food(4, 2, 1, 20), new Food(12, 5, 1, 30)], radius: 5);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 120.0, 130.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        Assert.Empty(simulation.State.World.FoodItems);
    }

    [Fact]
    public void SingleContestedFoodCannotFeedTwoOrganisms()
    {
        var simulation = SurvivalFixture.Create([new Organism(2, 1, 1, 0, 100), new Organism(9, 1, 1, 0, 100)],
            [new Food(4, 1, 1, 20)]);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 120.0, 100.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        Assert.Empty(simulation.State.World.FoodItems);
    }

    [Fact]
    public void SurvivalDoesNotChangeTask4ReflectedMovement()
    {
        var survival = SurvivalFixture.Create([new Organism(0, 9, 5, 0, 100)], drain: 2, speed: 3, width: 10, height: 10);
        var movement = MovementTests.Make(9, 5, 0, 3, 1);
        survival.AdvanceTick(); movement.AdvanceTick();
        var actual = survival.State.World.Organisms[0];
        var expected = movement.State.World.Organisms[0];
        Assert.Equal((expected.X, expected.Y, expected.HeadingRadians), (actual.X, actual.Y, actual.HeadingRadians));
        Assert.Equal(8, actual.X); Assert.Equal(Math.PI, actual.HeadingRadians); Assert.Equal(98, actual.Energy);
    }
}
