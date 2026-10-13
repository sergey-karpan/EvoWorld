using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SurvivalNumericalTests
{
    [Fact]
    public void EnergyCostOverflowRollsBackEvenForEmptyWorld()
    {
        SurvivalFixture.AssertRollback(SurvivalFixture.Create([], drain: double.MaxValue, duration: 2));
    }

    [Fact]
    public void FeedingOverflowAfterEarlierFoodClaimRollsBackEverything()
    {
        var simulation = SurvivalFixture.Create(
            [new Organism(2, 1, 1, 0, 100), new Organism(9, 2, 2, 0, double.MaxValue)],
            [new Food(4, 1, 1, 20), new Food(12, 2, 2, double.MaxValue)]);
        SurvivalFixture.AssertRollback(simulation);
        Assert.Equal(100, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(2, simulation.State.World.FoodItems.Count);
    }

    [Fact]
    public void ReflectionFailureAfterEarlierFeedingRollsBackFoodAndEnergy()
    {
        var simulation = SurvivalFixture.Create([new Organism(2, 1, 0, 0, 100), new Organism(9, 1, 0, Math.PI / 2, 100)],
            [new Food(4, 2, 0, 20)], drain: 1, speed: 1, height: double.Epsilon);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void MovementFailureAfterEarlierDeathDoesNotCommitRemoval()
    {
        var simulation = SurvivalFixture.Create([new Organism(2, 1, 1, 0, 1), new Organism(9, 2, 2, 0, 100)],
            drain: 1, duration: 2, speed: double.MaxValue);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void LargeEuclideanDistanceDoesNotOverflowIntoIncorrectEligibility()
    {
        var quarter = double.MaxValue / 4;
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)],
            [new Food(1, quarter, quarter, 20)], radius: double.MaxValue / 2,
            width: double.MaxValue, height: double.MaxValue);
        simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void DistanceLargerThanMaximumDoubleIsSafelyIneligible()
    {
        var coordinate = Math.BitDecrement(double.MaxValue);
        var food = new Food(1, coordinate, coordinate, 20);
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)], [food],
            radius: double.MaxValue, width: double.MaxValue, height: double.MaxValue);
        simulation.AdvanceTick();
        Assert.Same(food, Assert.Single(simulation.State.World.FoodItems));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TinyDiagonalDoesNotRoundDownToRadius(bool largerRadius)
    {
        var epsilon = double.Epsilon;
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)],
            [new Food(1, epsilon, epsilon, 20)], radius: largerRadius ? 2 * epsilon : epsilon);
        simulation.AdvanceTick();
        Assert.Equal(largerRadius ? 0 : 1, simulation.State.World.FoodItems.Count);
    }

    [Fact]
    public void TinyUnequalDistancesDoNotBecomeAnArtificialIdTie()
    {
        var epsilon = double.Epsilon;
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)],
            [new Food(1, epsilon, epsilon, 30), new Food(9, epsilon, 0, 20)], radius: 2 * epsilon);
        simulation.AdvanceTick();
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(1, Assert.Single(simulation.State.World.FoodItems).Id);
    }

    [Fact]
    public void ZeroRadiusDoesNotMatchSmallestNonzeroDistance()
    {
        var simulation = SurvivalFixture.Create([new Organism(0, 0, 0, 0, 100)], [new Food(1, double.Epsilon, 0, 20)]);
        simulation.AdvanceTick();
        Assert.Single(simulation.State.World.FoodItems);
    }
}
