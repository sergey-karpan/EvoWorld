using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SteeringNumericalTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(double.MaxValue)]
    public void OverflowingSteeringProductSaturatesWithoutFailing(double timestep)
    {
        var actual = FoodSeeking.Steer(SteeringFixture.Organism(steering: double.MaxValue), [new Food(0, 9, 10, 20)], [false], timestep);
        Assert.Equal(Math.PI, actual);
    }

    [Fact]
    public void SubnormalCoordinatesStillHaveMeaningfulDirection()
    {
        var settings = SteeringFixture.Settings(food: 1, width: 2 * double.Epsilon, height: 2 * double.Epsilon);
        var simulation = SteeringFixture.Create([SteeringFixture.Organism(x: 0, y: 0, sensing: double.Epsilon, steering: 10)], [new Food(0, 0, double.Epsilon, 20)], settings);
        simulation.AdvanceTick(); Assert.Equal(Math.PI / 2, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void LargeFiniteCoordinatesAndRadiusAvoidSquaredDistanceOverflow()
    {
        var organism = SteeringFixture.Organism(x: 0, y: 0, sensing: double.MaxValue, steering: 10);
        Assert.Equal(Math.PI / 4, FoodSeeking.Steer(organism, [new Food(0, double.MaxValue / 4, double.MaxValue / 4, 20)], [false], 1));
        Assert.Equal(0, FoodSeeking.Steer(organism, [new Food(0, double.MaxValue, double.MaxValue, 20)], [false], 1));
    }

    [Fact]
    public void TinyTurnUnderflowPreservesExactHeading()
    {
        var organism = SteeringFixture.Organism(heading: 0.7, steering: double.Epsilon);
        Assert.Equal(0.7, FoodSeeking.Steer(organism, [new Food(0, 9, 10, 20)], [false], double.Epsilon));
    }
}
