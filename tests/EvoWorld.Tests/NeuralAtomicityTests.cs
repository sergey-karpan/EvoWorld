using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralAtomicityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealEffectiveSpeedOrDistanceOverflowAfterEarlierFoodClaimRollsBack(bool distanceOverflow)
    {
        var brain = NeuralFixture.Brain(speedBias: distanceOverflow ? 0 : 8);
        var organisms = new[] { NeuralFixture.Organism(speed: 0, metabolism: 0, id: 0),
            NeuralFixture.Organism(brain, id: 9, x: 60, speed: double.MaxValue, metabolism: 0) };
        var simulation = NeuralFixture.Create(organisms, [new Food(0, 50, 40, 20)],
            NeuralFixture.Settings(organisms: 2, food: 1, duration: distanceOverflow ? 2 : 0.001));
        NeuralFixture.Rollback<OverflowException>(simulation);
        NeuralFixture.Rollback<OverflowException>(simulation); // Repeated failure is deterministic and leaks nothing.
        Assert.Equal(100, simulation.State.World.Organisms[0].Energy); Assert.Single(simulation.State.World.FoodItems);
    }

    [Theory]
    [InlineData("world")]
    [InlineData("mutation")]
    [InlineData("offspring")]
    public void FailureAfterTentativeFoodAndBirthDrawsPreservesBrainsAndRetry(string failure)
    {
        Simulation Create() => NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5, 0.25), speed: 0, energy: 200),
            NeuralFixture.Organism(NeuralFixture.Brain(-0.5, -0.25), id: 9, speed: 0, energy: 200)],
            settings: NeuralFixture.Settings(organisms: 2, foodCap: 1, cap: 4));
        var simulation = Create(); var clean = Create();
        simulation.FailWorldConstructionForTesting = failure == "world";
        simulation.FailBeforeMutationForTesting = failure == "mutation";
        simulation.FailOffspringTraitConstructionForTesting = failure == "offspring";
        if (failure == "world") NeuralFixture.Rollback<ArgumentNullException>(simulation);
        else if (failure == "mutation") NeuralFixture.Rollback<InvalidOperationException>(simulation);
        else NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        simulation.FailWorldConstructionForTesting = false; simulation.FailBeforeMutationForTesting = false;
        simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick(); NeuralFixture.SameState(clean, simulation);
        Assert.Equal(2, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void ExistingSteeringFailureSeamAlsoRollsBackNeuralWorkAndRetries()
    {
        Simulation Create() => NeuralFixture.Create([NeuralFixture.Organism(speed: 0),
            NeuralFixture.Organism(NeuralFixture.Brain(8), id: 9, speed: 0)],
            [new Food(0, 50, 40, 20)], NeuralFixture.Settings(organisms: 2, food: 1));
        var simulation = Create(); var clean = Create();
        simulation.FailSteeringForOrganismIdForTesting = 9; NeuralFixture.Rollback<InvalidOperationException>(simulation);
        simulation.FailSteeringForOrganismIdForTesting = null;
        simulation.AdvanceTick(); clean.AdvanceTick(); NeuralFixture.SameState(clean, simulation);
    }

    [Fact]
    public void MetabolicOverflowKeepsNeuralStateUnchanged()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(metabolism: double.MaxValue)],
            settings: NeuralFixture.Settings(duration: 2));
        NeuralFixture.Rollback<OverflowException>(simulation);
    }

    [Fact]
    public void NonfiniteSensorResultIsRejectedWithoutModifyingBrain()
    {
        // Invalid coordinates are not admitted by Simulation's custom-world seam.
        // Exercise the encoder's finite-channel guard directly, without a new failure seam.
        var brain = NeuralFixture.Brain(8, -8); var weights = brain.Weights.ToArray();
        Assert.Throws<OverflowException>(() => NeuralFixture.Inputs(NeuralFixture.Organism(brain, x: double.NaN)));
        Assert.Equal(weights, brain.Weights.ToArray());
        Assert.Throws<ArgumentException>(() => NeuralFixture.Create([NeuralFixture.Organism(brain, x: double.NaN)]));
    }

    [Fact]
    public void TickCountOverflowPreservesNeuralSnapshot()
    {
        var settings = NeuralFixture.Settings(); var organism = NeuralFixture.Organism(NeuralFixture.Brain(0.5));
        var simulation = new Simulation(settings, new WorldState(100, 80, [organism], []), tickCount: long.MaxValue);
        NeuralFixture.Rollback<OverflowException>(simulation);
    }
}
