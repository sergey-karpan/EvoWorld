using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class FoodRegenerationTests
{
    internal static SimulationSettings Settings(double duration = 1, double interval = 1, int cap = 10,
        int organisms = 0, int food = 0, double width = 100, double height = 80,
        double drain = 0, double radius = 0) =>
        new(42, duration, width, height, organisms, food, 0, drain, radius, interval, cap);

    [Fact]
    public void SkippedEventIsNotRecoveredWhenCapacityOpensBetweenEvents()
    {
        var settings = new SimulationSettings(42, 1, 100, 80, 1, 1, 1, 0, 0, 2, 1);
        var simulation = new Simulation(settings, new WorldState(100, 80,
            [new Organism(0, 0, 1, 0, 100)], [new Food(0, 3, 1, 20)]));
        var random = simulation.RandomState;
        simulation.AdvanceTick(); simulation.AdvanceTick(); // Tick 2 is cap-blocked.
        Assert.Equal(random, simulation.RandomState);
        simulation.AdvanceTick(); // Consumption at tick 3 does not recover tick 2.
        Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(random, simulation.RandomState);
        simulation.AdvanceTick();
        Assert.Equal(1, Assert.Single(simulation.State.World.FoodItems).Id);
    }

    [Fact]
    public void OverlappingSpawnsRequireExactlyTwoDrawsEachWithoutRetries()
    {
        var simulation = new Simulation(Settings(width: double.Epsilon, height: double.Epsilon));
        var random = new Pcg32(42);
        for (var tick = 1; tick <= 3; tick++)
        {
            simulation.AdvanceTick(); random.NextUInt(); random.NextUInt();
            Assert.Equal(random.CaptureState(), simulation.RandomState);
            Assert.Equal(tick, simulation.State.World.FoodItems.Count);
            Assert.All(simulation.State.World.FoodItems, food => Assert.Equal((0.0, 0.0), (food.X, food.Y)));
        }
    }

    [Theory]
    [InlineData(2, 1.0 / 30.0, 60)]
    [InlineData(0.26, 0.1, 3)]
    [InlineData(2.5, 1, 3)]
    [InlineData(1.5, 1, 2)]
    [InlineData(0.25, 0.1, 2)]
    [InlineData(0.01, 0.1, 1)]
    [InlineData(double.Epsilon, double.MaxValue, 1)]
    [InlineData(double.MaxValue, double.Epsilon, 0)]
    [InlineData(double.MaxValue, 1, 0)]
    [InlineData(9223372036854775808.0, 1, 0)]
    [InlineData(9223372036854774784.0, 1, 9223372036854774784L)]
    [InlineData(double.Epsilon, double.Epsilon, 1)]
    [InlineData(double.MaxValue, double.MaxValue, 1)]
    public void ScheduleUsesExactBinaryInputsAndHalfUpRounding(double interval, double timestep, long expected)
    {
        // 0.1 is slightly greater than decimal 0.1: exact 0.25 / supplied 0.1
        // is just below 2.5, even though ordinary double division rounds to 2.5.
        Assert.Equal(expected, FoodRegenerationSchedule.Calculate(interval, timestep));
    }

    [Fact]
    public void NeighborsOfHalfwayRoundOnCorrectSide()
    {
        Assert.Equal(2, FoodRegenerationSchedule.Calculate(Math.BitDecrement(2.5), 1));
        Assert.Equal(3, FoodRegenerationSchedule.Calculate(Math.BitIncrement(2.5), 1));
    }

    [Fact]
    public void DefaultCadenceHasNoInitialSpawnAndEventsAtSixtyTickMultiples()
    {
        var simulation = new Simulation(Settings(duration: 1.0 / 30, interval: 2));
        Assert.Empty(simulation.State.World.FoodItems);
        var random = new Pcg32(42);
        for (var tick = 1; tick <= 181; tick++)
        {
            simulation.AdvanceTick();
            if (tick % 60 == 0) { random.NextUInt(); random.NextUInt(); }
            Assert.Equal(tick / 60, simulation.State.World.FoodItems.Count);
            Assert.Equal(random.CaptureState(), simulation.RandomState);
        }
    }

    [Theory]
    [InlineData(0.26, 0.1, 3)]
    [InlineData(0.25, 0.1, 2)]
    [InlineData(2.5, 1, 3)]
    [InlineData(0.01, 0.1, 1)]
    public void RoundedPeriodControlsActualEvents(double interval, double timestep, int period)
    {
        var simulation = new Simulation(Settings(duration: timestep, interval: interval, cap: 100));
        for (var tick = 1; tick <= 12; tick++)
        {
            simulation.AdvanceTick();
            Assert.Equal(tick / period, simulation.State.World.FoodItems.Count);
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    public void CeilingPreservesExistingFoodAndOnlyPermittedSpawnsDraw(int initial, int cap, int expected)
    {
        var simulation = new Simulation(Settings(food: initial, cap: cap));
        var original = simulation.State;
        var random = new Pcg32(simulation.RandomState);
        if (initial < cap) { random.NextUInt(); random.NextUInt(); }
        simulation.AdvanceTick();
        Assert.Equal(expected, simulation.State.World.FoodItems.Count);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
        for (var i = 0; i < initial; i++) Assert.Same(original.World.FoodItems[i], simulation.State.World.FoodItems[i]);
        for (var tick = 0; tick < 5; tick++) simulation.AdvanceTick();
        Assert.Equal(expected, simulation.State.World.FoodItems.Count);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void ConsumptionCreatesCapacityAndSpawnCannotFeedUntilFollowingTick()
    {
        var settings = Settings(organisms: 1, food: 1, cap: 1, radius: 1000);
        var simulation = new Simulation(settings, new WorldState(100, 80,
            [new Organism(2, 1, 1, 0, 100)], [new Food(4, 1, 1, 20)]));
        simulation.AdvanceTick();
        Assert.Equal(120, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(5, Assert.Single(simulation.State.World.FoodItems).Id);
        simulation.AdvanceTick();
        Assert.Equal(140, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(6, Assert.Single(simulation.State.World.FoodItems).Id);
    }

    [Fact]
    public void SpawnCoordinatesAndStreamStateMatchIndependentPythonReference()
    {
        // Independent Python PCG-XSH-RR reference, seed=42, initseq=1.
        // Two initialization placement draws precede the regenerated food.
        var simulation = new Simulation(Settings(food: 1));
        Assert.Equal(new Pcg32State(3592518275746478678UL, 3), simulation.RandomState);
        simulation.AdvanceTick();
        var first = simulation.State.World.FoodItems[1];
        Assert.Equal((1, 34.73757542669773, 76.21518762782216, 20.0), (first.Id, first.X, first.Y, first.Energy));
        Assert.Equal(new Pcg32State(15941034996092842960UL, 3), simulation.RandomState);
        simulation.AdvanceTick();
        var second = simulation.State.World.FoodItems[2];
        Assert.Equal((2, 90.39041670039296, 33.434928208589554, 20.0), (second.Id, second.X, second.Y, second.Energy));
        Assert.Equal(new Pcg32State(13903747487881639386UL, 3), simulation.RandomState);
    }

    [Theory]
    [InlineData(double.Epsilon, double.Epsilon)]
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(double.MaxValue, double.Epsilon)]
    public void SpawnCoordinatesRespectExtremeHalfOpenBounds(double width, double height)
    {
        var simulation = new Simulation(Settings(width: width, height: height));
        simulation.AdvanceTick();
        var food = Assert.Single(simulation.State.World.FoodItems);
        Assert.True(double.IsFinite(food.X) && food.X >= 0 && food.X < width);
        Assert.True(double.IsFinite(food.Y) && food.Y >= 0 && food.Y < height);
        Assert.Equal(20, food.Energy);
        if (height == double.Epsilon) Assert.Equal(0, food.Y); // Rounded upper bound is corrected.
    }

    [Fact]
    public void CoordinateMappingIncludesZeroWithoutExtraDraws()
    {
        var random = new Pcg32(new Pcg32State(0, 3));
        Assert.Equal(0, WorldInitializer.NextScaled(random, 100));
        Assert.Equal(new Pcg32State(3, 3), random.CaptureState());
    }

    [Theory]
    [InlineData(double.MaxValue, double.Epsilon)]
    [InlineData(double.MaxValue, 1)]
    public void BeyondLifetimeIntervalsNeverSpawnEvenAtLastSupportedTick(double interval, double timestep)
    {
        var settings = Settings(duration: timestep, interval: interval);
        var simulation = new Simulation(settings, new WorldState(100, 80, [], []), long.MaxValue - 1);
        var random = simulation.RandomState;
        simulation.AdvanceTick();
        Assert.Equal(long.MaxValue, simulation.TickCount);
        Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void SmallManualTimelineCombinesSurvivalConsumptionAndRegeneration()
    {
        var settings = Settings(interval: 2, cap: 1, organisms: 1, drain: 1, radius: 1000);
        var simulation = new Simulation(settings, new WorldState(100, 80, [new Organism(2, 1, 1, 0, 5)], []));
        double[] energy = [4, 3, 22, 21, 40, 39];
        int[][] foodIds = [[], [0], [], [1], [], [2]];
        for (var tick = 1; tick <= 6; tick++)
        {
            simulation.AdvanceTick();
            Assert.Equal(energy[tick - 1], Assert.Single(simulation.State.World.Organisms).Energy);
            Assert.Equal(foodIds[tick - 1], simulation.State.World.FoodItems.Select(f => f.Id).ToArray());
        }
    }

    [Fact]
    public void ExtinctionDoesNotStopScheduleAndCeilingBoundsGrowth()
    {
        var settings = Settings(interval: 2, cap: 3, organisms: 1, drain: 1);
        var simulation = new Simulation(settings, new WorldState(100, 80, [new Organism(0, 1, 1, 0, 1)], []));
        for (var tick = 1; tick <= 20; tick++)
        {
            simulation.AdvanceTick();
            Assert.Empty(simulation.State.World.Organisms);
            Assert.Equal(Math.Min(3, tick / 2), simulation.State.World.FoodItems.Count);
            Assert.Equal(tick, simulation.TickCount);
        }
    }

    [Fact]
    public void DefaultWorldHistoriesRemainReproducibleForFiveThousandTicks()
    {
        var first = new Simulation(SimulationSettings.CreateDefault());
        var second = new Simulation(SimulationSettings.CreateDefault());
        var expectedRandom = new Pcg32(42);
        for (var i = 0; i < 260; i++) expectedRandom.NextUInt();
        var previousId = first.NextFoodId;
        for (var tick = 1; tick <= 5000; tick++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            if (first.NextFoodId != previousId)
            {
                Assert.Equal(previousId + 1, first.NextFoodId);
                Assert.Equal(0, tick % 60);
                Assert.Equal(previousId, first.State.World.FoodItems.Last().Id);
                expectedRandom.NextUInt(); expectedRandom.NextUInt();
                previousId = first.NextFoodId;
            }
            Assert.Equal(tick, first.TickCount);
            Assert.Equal(first.TickCount, second.TickCount);
            Assert.Equal(first.NextFoodId, second.NextFoodId);
            Assert.Equal(first.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray(),
                second.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray());
            Assert.Equal(first.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray(),
                second.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray());
            Assert.Equal(expectedRandom.CaptureState(), first.RandomState);
            Assert.Equal(first.RandomState, second.RandomState);
            Assert.InRange(first.State.World.FoodItems.Count, 0, 200);
        }
    }
}
