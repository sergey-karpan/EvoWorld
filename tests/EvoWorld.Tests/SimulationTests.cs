using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class SimulationTests
{
    [Fact]
    public void ConstructorRejectsNullSettings()
    {
        Assert.Equal("settings", Assert.Throws<ArgumentNullException>(() => new Simulation(null!)).ParamName);
    }

    [Fact]
    public void InitialWorldMatchesPublicInitializerAndRetainsPostInitializationRandomState()
    {
        var settings = new SimulationSettings(42UL, 0.125, 100, 80, 2, 2);
        var simulation = new Simulation(settings);
        Assert.Equal(0, simulation.TickCount);
        Assert.Same(settings, simulation.Settings);
        AssertWorldEqual(WorldInitializer.Create(settings), simulation.State.World);
        var expected = new Pcg32(settings.Seed);
        for (var i = 0; i < 10; i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public void EmptyCategoriesAdvanceNormally(int organisms, int food)
    {
        var simulation = new Simulation(new SimulationSettings(0UL, 0.125, 100, 80, organisms, food, 50, 1, 8, 2, 0, 150, 50, 50, 0));
        simulation.AdvanceTick();
        Assert.Equal(1, simulation.TickCount);
        Assert.Equal(organisms, simulation.State.World.Organisms.Count);
        Assert.Equal(food, simulation.State.World.FoodItems.Count);
    }

    [Fact]
    public void RepeatedTicksRemainDeterministicAndConsumeNoRandomValues()
    {
        // Preserve this Task 4 movement/RNG regression independently of survival.
        var settings = new SimulationSettings(42UL, 0.125, 10, 8, 10, 5, 50, 0, 0, 2, 0, 150, 50, 50, 0,
            0, 0, 0, 200, 0, 5, 1, 0.05, 0, 0, 0, 0, 0, 100, 0, 5, 0, 0);
        var first = new Simulation(settings);
        var second = new Simulation(settings);
        var initialRandom = first.RandomState;
        var initial = first.State;
        for (var tick = 1; tick <= 100; tick++)
        {
            first.AdvanceTick();
            second.AdvanceTick();
            Assert.Equal(tick, first.TickCount);
            Assert.Equal(first.TickCount, second.TickCount);
            AssertWorldEqual(first.State.World, second.State.World);
            Assert.Equal(initialRandom, first.RandomState);
            foreach (var organism in first.State.World.Organisms)
            {
                Assert.True(double.IsFinite(organism.X) && organism.X >= 0 && organism.X < 10);
                Assert.True(double.IsFinite(organism.Y) && organism.Y >= 0 && organism.Y < 8);
                Assert.True(double.IsFinite(organism.HeadingRadians) && organism.HeadingRadians >= 0
                    && organism.HeadingRadians < 2 * Math.PI);
                Assert.Equal(initial.World.Organisms[organism.Id].Energy, organism.Energy);
            }
            Assert.Equal(initial.World.FoodItems.ToArray(), first.State.World.FoodItems.ToArray());
        }
        Assert.Equal(0, initial.TickCount);
    }

    [Fact]
    public void InternalInitializerUsesSuppliedStreamWithoutReseeding()
    {
        var settings = new SimulationSettings(42UL, 1, 100, 80, 2, 2);
        var supplied = new Pcg32(999UL);
        var expected = new Pcg32(999UL);
        for (var i = 0; i < 7; i++) { supplied.NextUInt(); expected.NextUInt(); }
        var world = WorldInitializer.Create(settings, supplied);
        foreach (var organism in world.Organisms)
        {
            Assert.Equal(expected.NextUInt() / 4294967296.0 * 100, organism.X);
            Assert.Equal(expected.NextUInt() / 4294967296.0 * 80, organism.Y);
            Assert.Equal(expected.NextUInt() / 4294967296.0 * (2 * Math.PI), organism.HeadingRadians);
        }
        foreach (var food in world.FoodItems)
        {
            Assert.Equal(expected.NextUInt() / 4294967296.0 * 100, food.X);
            Assert.Equal(expected.NextUInt() / 4294967296.0 * 80, food.Y);
        }
        Assert.Equal(expected.CaptureState(), supplied.CaptureState());
        Assert.Throws<ArgumentNullException>(() => WorldInitializer.Create(settings, null!));
    }

    [Fact]
    public void DistanceOverflowLeavesWholeSimulationUnchanged()
    {
        var settings = new SimulationSettings(42UL, 2, 10, 10, 2, 1, double.MaxValue, 1, 8, 2, 0, 150, 50, 50, 0);
        var simulation = new Simulation(settings);
        AssertRollback(simulation);
    }

    [Fact]
    public void CandidateOverflowAfterValidOrganismDoesNotPartiallyCommit()
    {
        var settings = new SimulationSettings(42UL, 1, double.MaxValue, double.MaxValue, 2, 0, double.MaxValue, 1, 8, 2, 0, 150, 50, 50, 0);
        var world = new WorldState(double.MaxValue, double.MaxValue,
            [new Organism(0, 0, 0, Math.PI, 100, double.MaxValue, 1), new Organism(1, double.MaxValue / 2, 0, 0, 100, double.MaxValue, 1)], []);
        // First organism is representable; the second candidate overflows.
        AssertRollback(new Simulation(settings, world));
    }

    [Fact]
    public void UnsupportedReflectionAfterValidOrganismDoesNotPartiallyCommit()
    {
        var settings = new SimulationSettings(42UL, 1, 10, double.Epsilon, 2, 0, 1, 1, 8, 2, 0, 150, 50, 50, 0);
        var world = new WorldState(10, double.Epsilon,
            [new Organism(0, 1, 0, 0, 100, 1, 1), new Organism(1, 1, 0, Math.PI / 2, 100, 1, 1)], []);
        AssertRollback(new Simulation(settings, world));
    }

    private static void AssertRollback(Simulation simulation)
    {
        var previous = simulation.State;
        var random = simulation.RandomState;
        Assert.Throws<OverflowException>(simulation.AdvanceTick);
        Assert.Same(previous, simulation.State);
        Assert.Same(previous.World, simulation.State.World);
        Assert.Equal(previous.TickCount, simulation.TickCount);
        Assert.Equal(random, simulation.RandomState);
    }

    private static void AssertWorldEqual(WorldState first, WorldState second)
    {
        Assert.Equal(first.WorldWidth, second.WorldWidth);
        Assert.Equal(first.WorldHeight, second.WorldHeight);
        Assert.Equal(first.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray(),
            second.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray());
        Assert.Equal(first.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray(),
            second.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray());
    }
}
