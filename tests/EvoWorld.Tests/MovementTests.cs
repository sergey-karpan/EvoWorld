using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class MovementTests
{
    [Theory]
    [InlineData(0, 7, 5)]
    [InlineData(Math.PI / 2, 5, 7)]
    [InlineData(Math.PI, 3, 5)]
    [InlineData(3 * Math.PI / 2, 5, 3)]
    public void CardinalMovementUsesHeadingConvention(double heading, double x, double y)
    {
        var simulation = Make(5, 5, heading, 2, 1);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(x, moved.X);
        Assert.Equal(y, moved.Y);
        Assert.Equal(heading, moved.HeadingRadians);
        Assert.Equal(0, moved.Id);
        Assert.Equal(100.0, moved.Energy);
    }

    [Theory]
    [InlineData(0.25, 5.5)]
    [InlineData(0.5, 6.0)]
    public void TickUsesConfiguredDuration(double duration, double expectedX)
    {
        var simulation = Make(5, 5, 0, 2, duration);
        simulation.AdvanceTick();
        Assert.Equal(expectedX, simulation.State.World.Organisms[0].X);
        Assert.Equal(1, simulation.TickCount);
    }

    [Fact]
    public void DiagonalMovementMatchesIndependentPythonFixture()
    {
        // Python math.cos/sin(pi/4) * (50 * (1/30)), then add (5,5).
        var simulation = Make(5, 5, Math.PI / 4, 50, 1.0 / 30.0);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(6.178511301977579, moved.X, 12);
        Assert.Equal(6.178511301977579, moved.Y, 12);
        Assert.Equal(Math.PI / 4, moved.HeadingRadians);
    }

    [Fact]
    public void DiagonalVerticalReflectionMatchesIndependentFixtureAndContinuesInward()
    {
        // Python fixture: start (9,5), d=50/30, dx=dy=1.1785113019775793.
        var simulation = Make(9, 5, Math.PI / 4, 50, 1.0 / 30.0);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(9.82148869802242, moved.X, 12);
        Assert.Equal(6.178511301977579, moved.Y, 12);
        Assert.Equal(2.356194490192345, moved.HeadingRadians, 14);
        simulation.AdvanceTick();
        Assert.True(simulation.State.World.Organisms[0].X < moved.X);
    }

    [Theory]
    [InlineData(1, 5, Math.PI, 0, 5, 0)]
    [InlineData(9, 5, 0, 10, 5, Math.PI)]
    [InlineData(5, 1, 3 * Math.PI / 2, 5, 0, Math.PI / 2)]
    [InlineData(5, 9, Math.PI / 2, 5, 10, 3 * Math.PI / 2)]
    public void ExactWallHitsReflectDirection(double x, double y, double heading,
        double expectedX, double expectedY, double expectedHeading)
    {
        var simulation = Make(x, y, heading, 1, 1);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(expectedX == 10 ? Math.BitDecrement(10.0) : expectedX, moved.X);
        Assert.Equal(expectedY == 10 ? Math.BitDecrement(10.0) : expectedY, moved.Y);
        Assert.Equal(expectedHeading, moved.HeadingRadians);
        simulation.AdvanceTick();
        var next = simulation.State.World.Organisms[0];
        Assert.True(next.X >= 0 && next.X < 10 && next.Y >= 0 && next.Y < 10);
        Assert.Equal(expectedHeading, next.HeadingRadians);
    }

    [Theory]
    [InlineData(0, 5, 0, 1, 5, 0)]
    [InlineData(0, 5, Math.PI, 1, 5, 0)]
    [InlineData(5, 0, Math.PI / 2, 5, 1, Math.PI / 2)]
    [InlineData(5, 0, 3 * Math.PI / 2, 5, 1, Math.PI / 2)]
    [InlineData(0, 5, Math.PI / 2, 0, 6, Math.PI / 2)]
    [InlineData(5, 0, 0, 6, 0, 0)]
    public void StartingWallsRespectInwardOutwardAndParallelMotion(double x, double y, double heading,
        double expectedX, double expectedY, double expectedHeading)
    {
        var simulation = Make(x, y, heading, 1, 1);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(expectedX, moved.X);
        Assert.Equal(expectedY, moved.Y);
        Assert.Equal(expectedHeading, moved.HeadingRadians);
    }

    [Fact]
    public void BothAxesReflectAtCorner()
    {
        var simulation = Make(9.5, 9.5, Math.PI / 4, 2, 1);
        simulation.AdvanceTick();
        var moved = simulation.State.World.Organisms[0];
        Assert.Equal(5 * Math.PI / 4, moved.HeadingRadians);
        Assert.True(moved.X < 9.5 && moved.Y < 9.5);
    }

    [Fact]
    public void ZeroSpeedPreservesWallHeadingPositionAndFood()
    {
        var simulation = Make(0, 0, Math.PI, 0, 1);
        var previous = simulation.State;
        simulation.AdvanceTick();
        var entity = simulation.State.World.Organisms[0];
        Assert.Equal(0.0, entity.X);
        Assert.Equal(0.0, entity.Y);
        Assert.Equal(Math.PI, entity.HeadingRadians);
        Assert.Equal(100.0, entity.Energy);
        Assert.Same(previous.World.FoodItems[0], simulation.State.World.FoodItems[0]);
        Assert.Equal(1, simulation.TickCount);
    }

    [Theory]
    [InlineData(0, 5, 3 * Math.PI / 2)]
    [InlineData(5, 0, Math.PI)]
    public void NegativeParallelMovementDoesNotReverseOtherAxis(double x, double y, double heading)
    {
        var simulation = Make(x, y, heading, 1, 1);
        simulation.AdvanceTick();
        Assert.Equal(heading, simulation.State.World.Organisms[0].HeadingRadians);
        if (x == 0) Assert.Equal(0.0, simulation.State.World.Organisms[0].X);
        if (y == 0) Assert.Equal(0.0, simulation.State.World.Organisms[0].Y);
    }

    [Fact]
    public void ParallelMotionAtExclusiveUpperWallDoesNotReverseHeading()
    {
        var simulation = Make(Math.BitDecrement(10.0), 5, Math.PI / 2, 1, 1);
        simulation.AdvanceTick();
        Assert.Equal(Math.BitDecrement(10.0), simulation.State.World.Organisms[0].X);
        Assert.Equal(6.0, simulation.State.World.Organisms[0].Y);
        Assert.Equal(Math.PI / 2, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Theory]
    [InlineData(0, 1005, 7, 0)]
    [InlineData(0, 1015, 3, Math.PI)]
    [InlineData(Math.PI, 1005, 3, 0)]
    public void LargeBounceMovementMatchesIndependentIntegerFixtures(double heading, double speed,
        double expectedX, double expectedHeading)
    {
        var simulation = Make(2, 5, heading, speed, 1);
        simulation.AdvanceTick();
        Assert.Equal(expectedX, simulation.State.World.Organisms[0].X);
        Assert.Equal(5.0, simulation.State.World.Organisms[0].Y);
        Assert.Equal(expectedHeading, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void RepresentableTicksSucceedForSmallestAndLargestDimensions()
    {
        var tiny = Make(0, 0, 0, double.Epsilon, 1, double.Epsilon, double.Epsilon);
        tiny.AdvanceTick();
        Assert.Equal(0.0, tiny.State.World.Organisms[0].X);
        Assert.Equal(Math.PI, tiny.State.World.Organisms[0].HeadingRadians);
        var huge = Make(0, 0, 0, double.MaxValue, 1, double.MaxValue, double.MaxValue);
        huge.AdvanceTick();
        Assert.Equal(Math.BitDecrement(double.MaxValue), huge.State.World.Organisms[0].X);
        Assert.Equal(Math.PI, huge.State.World.Organisms[0].HeadingRadians);
    }

    internal static Simulation Make(double x, double y, double heading, double speed, double duration,
        double width = 10, double height = 10)
    {
        // Movement-only fixtures disable metabolism and avoid coincident food.
        // An epsilon-sized world has only coordinate zero, so it contains no food.
        Food[] food = width == double.Epsilon && height == double.Epsilon
            ? [] : [new Food(0, width / 3, height / 3, 20)];
        var settings = new SimulationSettings(42UL, duration, width, height, 1, food.Length, speed, 0, 0, 2, 0, 150, 50, 50, 0);
        var world = new WorldState(width, height,
            [new Organism(0, x, y, heading, 100, speed, 0)], food);
        return new Simulation(settings, world);
    }
}
