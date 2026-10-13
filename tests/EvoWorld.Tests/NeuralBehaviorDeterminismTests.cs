using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralBehaviorDeterminismTests
{
    [Fact]
    public void DisabledControlIgnoresAllBrainValuesAndMatchesLegacyBehavior()
    {
        var settings = NeuralFixture.Settings(enabled: false, organisms: 2, food: 2, duration: 0.1);
        Organism[] WithBrains(bool nonzero) => [NeuralFixture.Organism(nonzero ? NeuralFixture.Brain(8, 8) : null, id: 0),
            NeuralFixture.Organism(nonzero ? NeuralFixture.Brain(-8, -8) : null, id: 9)];
        Food[] food = [new(0, 50, 45, 20), new(9, 55, 40, 20)];
        var plain = NeuralFixture.Create(WithBrains(false), food, settings);
        var brains = NeuralFixture.Create(WithBrains(true), food, settings);
        for (var i = 0; i < 100; i++)
        {
            plain.AdvanceTick(); brains.AdvanceTick(); StatisticsFixture.AssertWorlds(plain, brains);
            StatisticsFixture.AssertBits(plain.GetStatistics(), brains.GetStatistics());
        }
    }

    [Fact]
    public void InputEncodingAndForwardPassConsumeNoRngAcrossTicks()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5, -0.25), metabolism: 0)],
            settings: NeuralFixture.Settings(duration: 0.01));
        var random = simulation.RandomState; var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId;
        for (var i = 0; i < 100; i++)
        {
            simulation.AdvanceTick(); Assert.Equal(random, simulation.RandomState);
            Assert.Equal(foodId, simulation.NextFoodId); Assert.Equal(organismId, simulation.NextOrganismId);
        }
    }

    [Fact]
    public void StatisticsKeepExistingAggregationAndNeverChangeNeuralWorldOrRng()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5, 0.5), speed: 1, metabolism: 1, sensing: 1, steering: 1),
            NeuralFixture.Organism(NeuralFixture.Brain(-0.5, -0.5), id: 9, speed: 3, metabolism: 3, sensing: 3, steering: 3)],
            settings: NeuralFixture.Settings(organisms: 2, duration: 0.01));
        simulation.AdvanceTick(); var state = simulation.State; var random = simulation.RandomState;
        var statistics = simulation.GetStatistics(out var snapshot); Assert.Same(state, snapshot);
        foreach (var trait in StatisticsFixture.Traits(statistics))
        {
            Assert.Equal(1, trait.Min); Assert.Equal(3, trait.Max); Assert.Equal(2, trait.Mean); Assert.Equal(1, trait.StandardDeviation);
        }
        Assert.Equal(1, statistics.TickCount); Assert.Equal(2, statistics.OrganismCount); Assert.Equal(0, statistics.TotalBirths); Assert.Equal(0, statistics.TotalDeaths);
        StatisticsFixture.AssertBits(statistics, simulation.GetStatistics()); Assert.Equal(random, simulation.RandomState); Assert.Same(state, simulation.State);
    }

    [Fact]
    public void InitializedNeuralWorldsReproduceCompleteStateFor5000Ticks()
    {
        var settings = NeuralFixture.Settings(organisms: 20, food: 100, duration: 1.0 / 30,
            width: 1000, height: 700, baselineSpeed: 50, pickup: 8, cap: 500, foodCap: 200, interval: 2);
        var first = new Simulation(settings); var second = new Simulation(settings);
        NeuralFixture.SameState(first, second);
        for (var i = 0; i < 5000; i++)
        {
            first.GetStatistics(); first.AdvanceTick(); second.AdvanceTick();
            if (i % 100 == 0) NeuralFixture.SameState(first, second);
        }
        Assert.Equal(5000, first.TickCount); NeuralFixture.SameState(first, second);
    }

    [Fact]
    public void InitializedBrainsRemainReproducibleThroughActiveBirthsFor5000Ticks()
    {
        var settings = NeuralFixture.Settings(organisms: 2, food: 10, duration: 0.001, cap: 10, foodCap: 20, interval: 0.01);
        Simulation Create()
        {
            var initialized = WorldInitializer.Create(settings);
            var organisms = initialized.Organisms.Select(o => new Organism(o.Id, o.X, o.Y, o.HeadingRadians, 100000,
                o.Speed, 0.001, o.SensingRadius, o.SteeringRate, o.Brain));
            return new Simulation(settings, new WorldState(100, 80, organisms, initialized.FoodItems));
        }
        var first = Create(); var second = Create();
        var ancestors = first.State.World.Organisms.Select(o => o.Brain).ToArray();
        var weights = ancestors.Select(b => b.Weights.ToArray()).ToArray();
        for (var i = 0; i < 5000; i++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            if (i % 100 == 0) NeuralFixture.SameState(first, second);
        }
        Assert.True(first.GetStatistics().TotalBirths > 0); Assert.Equal(5000, first.TickCount);
        NeuralFixture.SameState(first, second);
        Assert.All(first.State.World.Organisms, o => Assert.Contains(o.Brain, ancestors));
        for (var i = 0; i < ancestors.Length; i++) Assert.Equal(weights[i], ancestors[i].Weights.ToArray());
    }

    [Fact]
    public void ControllerUsesNoHeapScratchAfterWarmup()
    {
        var organism = NeuralFixture.Organism(NeuralFixture.Brain(0.5, -0.25)); var settings = NeuralFixture.Settings();
        Food[] food = [new(0, 55, 45, 20)]; bool[] consumed = [false];
        for (var i = 0; i < 10; i++) NeuralControl.Evaluate(organism, 99, food, consumed, settings);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) NeuralControl.Evaluate(organism, 99, food, consumed, settings);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
