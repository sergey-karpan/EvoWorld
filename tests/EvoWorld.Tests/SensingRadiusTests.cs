using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SensingRadiusTests
{
    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void InclusiveRadiusControlsDetection(double distance, double radius, bool detected)
    {
        var organism = SteeringFixture.Organism(sensing: radius, steering: 10);
        var simulation = SteeringFixture.Create([organism], [new Food(0, 10, 10 + distance, 20)]);
        var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Equal(detected ? Math.PI / 2 : 0, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Equal(random, simulation.RandomState); Assert.Single(simulation.State.World.FoodItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void CoincidentFoodPreservesHeadingAndPickupRemainsIndependent(double radius)
    {
        var simulation = SteeringFixture.Create([SteeringFixture.Organism(heading: 0.7, sensing: radius)], [new Food(0, 10, 10, 20)]);
        simulation.AdvanceTick(); Assert.Equal(0.7, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy); Assert.Empty(simulation.State.World.FoodItems);
    }

    [Fact]
    public void NearestThenSmallestIdWinsEvenWhenCollectionOrderDiffers()
    {
        var organism = SteeringFixture.Organism(steering: 10);
        Food[] foods = [new Food(9, 10, 11, 20), new Food(2, 11, 10, 20), new Food(1, 20, 10, 20)];
        Assert.Equal(1, FoodSeeking.FindNearest(10, 10, foods, [false, false, false], FoodDistance.FromLength(30)));
        Assert.Equal(0, FoodSeeking.Steer(organism, foods, [false, false, false], 1));
        Assert.Equal(Math.PI / 2, FoodSeeking.Steer(organism, foods, [false, true, false], 1));
    }

    [Fact]
    public void ZeroSensingAndNoFoodPreserveExactHeadingBits()
    {
        var heading = Math.BitDecrement(2 * Math.PI);
        var organism = SteeringFixture.Organism(heading: heading, sensing: 0);
        Assert.Equal(BitConverter.DoubleToInt64Bits(heading), BitConverter.DoubleToInt64Bits(FoodSeeking.Steer(organism, [new Food(0, 10, 11, 20)], [false], 1)));
        Assert.Equal(heading, FoodSeeking.Steer(SteeringFixture.Organism(heading: heading), [], [], 1));
    }
}
