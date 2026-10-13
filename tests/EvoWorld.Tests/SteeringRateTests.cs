using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SteeringRateTests
{
    [Theory]
    [InlineData(1, 1, Math.PI / 4)]
    [InlineData(-1, 1, 3 * Math.PI / 4)]
    [InlineData(-1, -1, 5 * Math.PI / 4)]
    [InlineData(1, -1, 7 * Math.PI / 4)]
    public void TargetHeadingsFollowScreenQuadrants(double dx, double dy, double expected)
    {
        var actual = FoodSeeking.Steer(SteeringFixture.Organism(steering: 10), [new Food(0, 10 + dx, 10 + dy, 20)], [false], 1);
        Assert.Equal(expected, actual, 14);
    }

    [Theory]
    [InlineData(0, 1, 0.25)]
    [InlineData(Math.PI, 1, Math.PI - 0.25)]
    [InlineData(0, -1, 2 * Math.PI - 0.25)]
    public void RotationIsRateTimesTimestepWithoutOvershoot(double heading, double dy, double expected)
    {
        var actual = FoodSeeking.Steer(SteeringFixture.Organism(heading: heading, steering: 1), [new Food(0, 10, 10 + dy, 20)], [false], 0.25);
        Assert.Equal(expected, actual, 14);
    }

    [Fact]
    public void HalfTurnTieAlwaysTurnsPositive()
    {
        Assert.Equal(1, FoodSeeking.Steer(SteeringFixture.Organism(steering: 1), [new Food(0, 9, 10, 20)], [false], 1));
        Assert.Equal(Math.PI + 1, FoodSeeking.Steer(SteeringFixture.Organism(heading: Math.PI, steering: 1), [new Food(0, 11, 10, 20)], [false], 1));
    }

    [Fact]
    public void WrapAndNoOvershootReachTargetAndZeroRateIsExact()
    {
        Assert.Equal(0, FoodSeeking.Steer(SteeringFixture.Organism(heading: 2 * Math.PI - 0.1, steering: 1), [new Food(0, 11, 10, 20)], [false], 1));
        Assert.Equal(0.3, FoodSeeking.Steer(SteeringFixture.Organism(heading: 0.3, steering: 0), [new Food(0, 11, 10, 20)], [false], 1));
        Assert.Equal(0.3, FoodSeeking.Steer(SteeringFixture.Organism(heading: 0.3), [new Food(0, 10 + Math.Cos(0.3), 10 + Math.Sin(0.3), 20)], [false], 1), 14);
    }

    [Fact]
    public void MovementUsesSteeredCardinalHeadingAndReflectionWins()
    {
        var simulation = SteeringFixture.Create([SteeringFixture.Organism(x: 99, y: 40, heading: Math.PI / 2, speed: 3, steering: 10)], [new Food(0, Math.BitDecrement(100.0), 40, 20)]);
        simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        Assert.Equal(98, organism.X); Assert.Equal(40, organism.Y); Assert.Equal(Math.PI, organism.HeadingRadians);
        Assert.Equal((3.0, 0.0, 30.0, 10.0), (organism.Speed, organism.Metabolism, organism.SensingRadius, organism.SteeringRate));
    }
}
