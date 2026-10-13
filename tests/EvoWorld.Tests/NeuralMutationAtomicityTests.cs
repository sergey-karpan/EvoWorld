using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralMutationAtomicityTests
{
    [Theory]
    [InlineData(0.125)]
    [InlineData(double.MaxValue)]
    public void WorldFailureAfterTwoMutatedChildrenAndFoodDrawsRollsBackAndRetries(double amplitude)
    {
        Simulation Create() => NeuralMutationFixture.Create([
            NeuralMutationFixture.Parent(new NeuralGenome(NeuralMutationGolden.ParentA)),
            NeuralMutationFixture.Parent(new NeuralGenome(NeuralMutationGolden.ParentB), id: 9, x: 60)],
            NeuralMutationFixture.Settings(amplitude, organisms: 2, cap: 4, foodCap: 1));
        var simulation = Create(); var clean = Create();
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 162; i++) reference.NextUInt();
        simulation.FailWorldConstructionForTesting = true;
        NeuralFixture.Rollback<ArgumentNullException>(simulation);
        simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick(); NeuralFixture.SameState(clean, simulation);
        Assert.Equal(reference.CaptureState(), simulation.RandomState); Assert.Equal(2, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void ExistingOffspringFailureAfterAllSeventyFourNeuralDrawsRetainsTransaction()
    {
        Simulation Create() => NeuralMutationFixture.Create([NeuralMutationFixture.Parent(new NeuralGenome(NeuralMutationGolden.ParentA))]);
        var simulation = Create(); var clean = Create();
        simulation.FailOffspringTraitConstructionForTesting = true;
        NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick(); NeuralFixture.SameState(clean, simulation);
    }

    [Fact]
    public void ExistingPreMutationFailureDoesNotLeakDrawsOrParentChanges()
    {
        Simulation Create() => NeuralMutationFixture.Create([NeuralMutationFixture.Parent()]);
        var simulation = Create(); var clean = Create();
        simulation.FailBeforeMutationForTesting = true; NeuralFixture.Rollback<InvalidOperationException>(simulation);
        simulation.FailBeforeMutationForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick(); NeuralFixture.SameState(clean, simulation);
    }

    [Fact]
    public void RealIdOverflowAfterFirstTentativeMutatedChildRollsBackEntireTick()
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(), NeuralMutationFixture.Parent(id: 9, x: 60)],
            NeuralMutationFixture.Settings(organisms: 2, cap: 4), nextOrganismId: int.MaxValue);
        NeuralFixture.Rollback<OverflowException>(simulation); NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Equal(int.MaxValue, simulation.NextOrganismId); Assert.Equal(0, simulation.GetStatistics().TotalBirths);
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(160, o.Energy));
    }

    [Fact]
    public void FailedFeedingPrecedesMutationAndPreservesFullHistory()
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(energy: double.MaxValue)],
            NeuralMutationFixture.Settings(food: 1), [new Food(0, 50, 40, double.MaxValue)]);
        NeuralFixture.Rollback<OverflowException>(simulation);
    }
}
