using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class InheritanceTests
{
    [Fact]
    public void BirthUsesPostReflectionPositionAndHeading()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 9, 5, 0, 160)], speed: 3, width: 10, height: 10);
        simulation.AdvanceTick();
        var parent = simulation.State.World.Organisms[0]; var child = simulation.State.World.Organisms[1];
        Assert.Equal(8, parent.X); Assert.Equal(Math.PI, parent.HeadingRadians);
        Assert.Equal(Math.BitDecrement(10.0), child.X);
        Assert.Equal(3.535499928060587, child.Y, 12);
        Assert.Equal(Math.PI + 0.08078083340078593, child.HeadingRadians, 14);
    }

    [Fact]
    public void ChildPositionAndHeadingMatchIndependentPythonFixture()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 1, 160)]);
        Assert.Equal(new Pcg32State(10355249797275478817UL, 3), simulation.RandomState);
        var random = new Pcg32(simulation.RandomState);
        Assert.Equal(4091771729U, random.NextUInt()); Assert.Equal(3882238836U, random.NextUInt()); random.NextUInt(); random.NextUInt();
        simulation.AdvanceTick();
        var child = simulation.State.World.Organisms[1];
        Assert.Equal(54.78071537944788, child.X, 12); Assert.Equal(38.53549992806059, child.Y, 12);
        Assert.Equal(1.080780833400786, child.HeadingRadians, 14);
        Assert.Equal(1, child.Id); Assert.Equal(50, child.Energy);
        Assert.Equal(1, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PlacementClampsAtEachBoundaryWithoutRetryDraws(int boundary)
    {
        var x = boundary == 1 ? Math.BitDecrement(100.0) : boundary == 2 ? 0 : 50;
        var y = boundary == 0 ? 0 : boundary == 3 ? Math.BitDecrement(80.0) : 40;
        Food[] food = boundary >= 2 ? [new Food(0, 99, 79, 20)] : [];
        var simulation = ReproductionFixture.Create([new Organism(0, x, y, 0, 160)], food);
        var random = new Pcg32(simulation.RandomState); random.NextUInt(); random.NextUInt(); random.NextUInt(); random.NextUInt();
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        if (boundary == 0) Assert.Equal(0, child.Y);
        if (boundary == 1) Assert.Equal(Math.BitDecrement(100.0), child.X);
        if (boundary == 2) Assert.Equal(0, child.X);
        if (boundary == 3) Assert.Equal(Math.BitDecrement(80.0), child.Y);
        Assert.True(child.X >= 0 && child.X < 100 && child.Y >= 0 && child.Y < 80);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(0.001)]
    [InlineData(double.MaxValue)]
    public void PlacementStaysFiniteInExtremeWorldsAndOverlapIsAllowed(double dimension)
    {
        var position = dimension == double.Epsilon ? 0 : Math.BitDecrement(dimension);
        var simulation = ReproductionFixture.Create([new Organism(0, position, position, 0, 160)], width: dimension, height: dimension);
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.True(double.IsFinite(child.X) && child.X >= 0 && child.X < dimension);
        Assert.True(double.IsFinite(child.Y) && child.Y >= 0 && child.Y < dimension);
        if (dimension == double.Epsilon) Assert.Equal((0.0, 0.0), (child.X, child.Y));
        if (dimension == double.MaxValue) Assert.Equal((position, position), (child.X, child.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeadingNormalizesAcrossBothEnds(bool negative)
    {
        var heading = negative ? 0.01 : 2 * Math.PI - 0.01;
        Food[] food = negative ? [new Food(0, 99, 79, 20), new Food(1, 98, 78, 20)] : [];
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, heading, 160)], food);
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.True(child.HeadingRadians >= 0 && child.HeadingRadians < 2 * Math.PI);
        if (negative) Assert.Equal(6.201875525387944, child.HeadingRadians, 13);
        else Assert.Equal(0.070780833400786, child.HeadingRadians, 13);
        Assert.Equal(heading, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void FoodRegenerationThenTwoBirthsConsumeTenOutputsInRequiredOrder()
    {
        var simulation = ReproductionFixture.Create([new Organism(2, 50, 40, 0, 160), new Organism(9, 60, 40, 1, 160)], foodCap: 10);
        Assert.Equal(new Pcg32State(13903747487881639386UL, 3), simulation.RandomState);
        simulation.AdvanceTick();
        var food = Assert.Single(simulation.State.World.FoodItems);
        Assert.Equal((52.76218126527965, 36.11299082636833), (food.X, food.Y));
        Assert.Equal(new[] { 2, 9, 10, 11 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        var first = simulation.State.World.Organisms[2]; var second = simulation.State.World.Organisms[3];
        Assert.Equal(54.81481665574779, first.X, 12); Assert.Equal(41.34816192333625, first.Y, 12);
        Assert.Equal(6.234506593142645, first.HeadingRadians, 14);
        Assert.Equal(64.37719989071232, second.X, 12); Assert.Equal(42.41663425382245, second.Y, 12);
        Assert.Equal(0.952093210304156, second.HeadingRadians, 14);
        Assert.Equal(new Pcg32State(11742290374445839356UL, 3), simulation.RandomState);
        Assert.Equal(new[] { 110.0, 110.0, 50.0, 50.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
    }
}
