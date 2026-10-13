using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralInputEncodingTests
{
    [Fact]
    public void IndependentThreeFourFiveTriangleChecksAllSixChannels()
    {
        // By hand: relative sin=4/5, cos=3/5; d/r=5/10;
        // E/100=3/4; speed/200=1/4; nearest wall/r=10/10.
        var inputs = NeuralFixture.Inputs(NeuralFixture.Organism(x: 10, y: 10, heading: 0, sensing: 10, speed: 50),
            [new Food(0, 13, 14, 20)], energy: 75);
        double[] expected = [0.8, 0.6, 0, 0.5, -0.5, -1];
        for (var i = 0; i < 6; i++) NeuralFixture.Near(expected[i], inputs[i]);
    }

    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, -1, -1, 0)]
    [InlineData(-1, 0, 0, -1)]
    public void RelativeBearingsUsePositiveHalfTurnConvention(double dx, double dy, double sin, double cos)
    {
        var inputs = NeuralFixture.Inputs(NeuralFixture.Organism(heading: 0), [new Food(0, 50 + dx, 40 + dy, 20)]);
        NeuralFixture.Near(sin, inputs[0]); NeuralFixture.Near(cos, inputs[1]);
        if (dx == -1) Assert.True(inputs[0] > 0); // sin(+pi), not sin(-pi).
    }

    [Theory]
    [InlineData(0, 51, 40)]
    [InlineData(1, 50, 41)]
    [InlineData(2, 49, 40)]
    [InlineData(3, 50, 39)]
    public void AheadEncodingIsIndependentOfCardinalHeading(int quarterTurns, double x, double y)
    {
        var inputs = NeuralFixture.Inputs(NeuralFixture.Organism(heading: quarterTurns * Math.PI / 2), [new Food(0, x, y, 20)]);
        NeuralFixture.Near(0, inputs[0]); NeuralFixture.Near(1, inputs[1]);
    }

    [Fact]
    public void NoFoodOutsideRadiusAndConsumedFoodHaveSameEncoding()
    {
        var organism = NeuralFixture.Organism();
        var empty = NeuralFixture.Inputs(organism);
        var outside = NeuralFixture.Inputs(organism, [new Food(0, 99, 40, 20)]);
        var consumed = NeuralFixture.Inputs(organism, [new Food(0, 50, 40, 20)], consumed: [true]);
        Assert.Equal(empty, outside); Assert.Equal(empty, consumed);
        Assert.Equal(new[] { 0.0, 1, -1 }, empty[..3]);
    }

    [Fact]
    public void PositiveRadiusCoincidenceUsesExplicitConventionAndDoesNotConsume()
    {
        var consumed = new[] { false };
        var inputs = NeuralFixture.Inputs(NeuralFixture.Organism(heading: 4), [new Food(0, 50, 40, 20)], consumed: consumed);
        Assert.Equal(new[] { 0.0, 1, 1 }, inputs[..3]); Assert.False(consumed[0]);
    }

    [Fact]
    public void ZeroRadiusDisablesSensingEvenForCoincidenceAndWallChannel()
    {
        var inputs = NeuralFixture.Inputs(NeuralFixture.Organism(x: 0, sensing: 0), [new Food(0, 0, 40, 20)]);
        Assert.Equal(new[] { 0.0, 1, -1 }, inputs[..3]); Assert.Equal(-1, inputs[5]);
    }

    [Fact]
    public void RadiusEdgeIsIncludedAndOneUlpBeyondIsExcluded()
    {
        var organism = NeuralFixture.Organism(heading: 0, sensing: 10);
        var edge = NeuralFixture.Inputs(organism, [new Food(0, 60, 40, 20)]);
        Assert.Equal(-1, edge[2]); Assert.Equal(1, edge[1]);
        var beyond = NeuralFixture.Inputs(organism, [new Food(0, Math.BitIncrement(60), 40, 20)]);
        Assert.Equal(new[] { 0.0, 1, -1 }, beyond[..3]);
    }

    [Fact]
    public void NearestAvailableFoodUsesSmallestIdAndDoesNotDependOnCollectionOrder()
    {
        var organism = NeuralFixture.Organism(heading: 0);
        Food[] food = [new(9, 50, 39, 20), new(2, 50, 41, 20), new(1, 51, 40, 20)];
        var tied = NeuralFixture.Inputs(organism, food); Assert.Equal(0, tied[0]); Assert.Equal(1, tied[1]);
        Assert.Equal(tied, NeuralFixture.Inputs(organism, food.Reverse().ToArray()));
        var remaining = NeuralFixture.Inputs(organism, food, consumed: [false, false, true]);
        NeuralFixture.Near(1, remaining[0]); NeuralFixture.Near(0, remaining[1]);
        var nearest = NeuralFixture.Inputs(organism, [new Food(1, 52, 40, 20), new Food(9, 50, 40.5, 20)]);
        NeuralFixture.Near(1, nearest[0]);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(25, -0.5)]
    [InlineData(50, 0)]
    [InlineData(100, 1)]
    [InlineData(200, 1)]
    [InlineData(double.MaxValue, 1)]
    public void EnergyIsPostDrainClampedToInitialEnergy(double energy, double expected)
    {
        Assert.Equal(expected, NeuralFixture.Inputs(NeuralFixture.Organism(), energy: energy)[3]);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(50, -0.5)]
    [InlineData(200, 1)]
    [InlineData(300, 1)]
    [InlineData(double.MaxValue, 1)]
    public void SpeedUsesConfiguredBoundsAndSaturatesCustomValues(double speed, double expected)
    {
        Assert.Equal(expected, NeuralFixture.Inputs(NeuralFixture.Organism(speed: speed))[4]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 0)]
    [InlineData(10, -1)]
    [InlineData(40, -1)]
    public void WallChannelUsesNearestWallAndSaturates(double x, double expected)
    {
        Assert.Equal(expected, NeuralFixture.Inputs(NeuralFixture.Organism(x: x, sensing: 10))[5]);
    }

    [Fact]
    public void UpperAndHorizontalWallsUseSameProximityRule()
    {
        Assert.Equal(1, NeuralFixture.Inputs(NeuralFixture.Organism(y: 0))[5]);
        NeuralFixture.Near(1, NeuralFixture.Inputs(NeuralFixture.Organism(x: Math.BitDecrement(100.0)))[5]);
        NeuralFixture.Near(1, NeuralFixture.Inputs(NeuralFixture.Organism(y: Math.BitDecrement(80.0)))[5]);
    }

    [Fact]
    public void HugeDistanceIsScaledAndOverflowingEuclideanMagnitudeIsRejectedByRadius()
    {
        var settings = NeuralFixture.Settings(width: double.MaxValue, height: double.MaxValue);
        var organism = NeuralFixture.Organism(x: 0, y: 0, heading: 0, sensing: double.MaxValue);
        var inputs = NeuralFixture.Inputs(organism, [new Food(0, double.MaxValue / 2, double.MaxValue / 2, 20)], settings);
        NeuralFixture.Near(Math.Sqrt(0.5), inputs[0]); NeuralFixture.Near(Math.Sqrt(0.5), inputs[1]);
        NeuralFixture.Near(1 - Math.Sqrt(2), inputs[2]);
        var outside = NeuralFixture.Inputs(organism, [new Food(0, double.MaxValue * 0.75, double.MaxValue * 0.75, 20)], settings);
        Assert.Equal(new[] { 0.0, 1, -1 }, outside[..3]);
    }

    [Fact]
    public void UnderflowedPositiveRatioDoesNotBecomeCoincidentDirection()
    {
        var organism = NeuralFixture.Organism(x: 0, y: 0, heading: 0, sensing: double.MaxValue);
        var inputs = NeuralFixture.Inputs(organism, [new Food(0, double.Epsilon, double.Epsilon, 20)]);
        NeuralFixture.Near(Math.Sqrt(0.5), inputs[0]); NeuralFixture.Near(Math.Sqrt(0.5), inputs[1]);
        Assert.Equal(1, inputs[2]);
    }

    [Fact]
    public void SubnormalRadiusAndHugeWallRatioStayFinite()
    {
        var organism = NeuralFixture.Organism(x: 0, y: 0, heading: 0, sensing: 2 * double.Epsilon);
        var inputs = NeuralFixture.Inputs(organism, [new Food(0, double.Epsilon, 0, 20)]);
        Assert.Equal(0, inputs[2]); Assert.Equal(1, inputs[5]);
        Assert.Equal(-1, NeuralFixture.Inputs(NeuralFixture.Organism(sensing: double.Epsilon))[5]);
    }

    [Fact]
    public void SpeedFractionsHandleHugeAdjacentAndSubnormalBounds()
    {
        var settings = NeuralFixture.Settings(maxSpeed: double.MaxValue);
        Assert.Equal(0, NeuralFixture.Inputs(NeuralFixture.Organism(speed: double.MaxValue / 2), settings: settings)[4]);
        var lower = Math.BitDecrement(Math.BitDecrement(double.MaxValue));
        settings = NeuralFixture.Settings(minSpeed: lower, maxSpeed: double.MaxValue, baselineSpeed: lower);
        Assert.Equal(0, NeuralFixture.Inputs(NeuralFixture.Organism(speed: Math.BitDecrement(double.MaxValue)), settings: settings)[4]);
        Assert.Equal(-1, NeuralFixture.Inputs(NeuralFixture.Organism(speed: 0), settings: settings)[4]);
        settings = NeuralFixture.Settings(maxSpeed: 4 * double.Epsilon, baselineSpeed: double.Epsilon);
        Assert.Equal(-0.5, NeuralFixture.Inputs(NeuralFixture.Organism(speed: double.Epsilon), settings: settings)[4]);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void NonfiniteOrInvalidEnergyIsRejectedBeforeClamping(double energy)
    {
        Assert.Throws<OverflowException>(() => NeuralFixture.Inputs(NeuralFixture.Organism(), energy: energy));
    }
}
