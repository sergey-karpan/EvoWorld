using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralMutationContractTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 0.125)]
    [InlineData(false, double.MaxValue)]
    [InlineData(true, 0)]
    public void InactiveMutationPreservesExactOutOfRangeBrainAndSixDraws(bool enabled, double amplitude)
    {
        var brain = new NeuralGenome(Enumerable.Repeat(8.0, 74).ToArray());
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(brain)], NeuralMutationFixture.Settings(amplitude, enabled));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) reference.NextUInt();
        simulation.AdvanceTick(); Assert.Same(brain, simulation.State.World.Organisms[1].Brain);
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(0.125)]
    [InlineData(double.MaxValue)]
    public void OneActiveBirthConsumesEightyOutputs(double amplitude)
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent()], NeuralMutationFixture.Settings(amplitude));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 80; i++) reference.NextUInt();
        simulation.AdvanceTick(); Assert.Equal(reference.CaptureState(), simulation.RandomState);
        Assert.Equal(1, simulation.GetStatistics().TotalBirths);
    }

    [Theory]
    [InlineData(100, 10, 50, 150)]
    [InlineData(160, 1, 50, 150)]
    [InlineData(160, 0, 50, 150)]
    [InlineData(150, 10, 150, 150)]
    public void EnergyCapAndNonpositiveRemainingEnergySkippedBirthsConsumeNothing(double energy, int cap, double cost, double threshold)
    {
        var parent = NeuralMutationFixture.Parent(energy: energy);
        var simulation = NeuralMutationFixture.Create([parent], NeuralMutationFixture.Settings(cap: cap, cost: cost, threshold: threshold));
        var random = simulation.RandomState; var id = simulation.NextOrganismId;
        simulation.AdvanceTick(); Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(id, simulation.NextOrganismId);
        Assert.Same(parent.Brain, simulation.State.World.Organisms[0].Brain); Assert.Equal(energy, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void MixedEligibleAndSkippedParentsOnlyConsumeSuccessfulBirthDraws()
    {
        var a = NeuralMutationFixture.Parent(energy: 100); var b = NeuralMutationFixture.Parent(id: 9, energy: 160, x: 60);
        var simulation = NeuralMutationFixture.Create([a, b], NeuralMutationFixture.Settings(organisms: 2, cap: 3));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) reference.NextUInt();
        var expected = NeuralMutationFixture.MutatedWeights(b.Brain, 0.125, -1, 1, reference);
        simulation.AdvanceTick(); Assert.Equal(3, simulation.State.World.Organisms.Count);
        NeuralMutationFixture.AssertBits(expected, simulation.State.World.Organisms[2].Brain);
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void CapStopsMutationForLaterEligibleParentWithoutConsumingItsDraws()
    {
        var a = new NeuralGenome(NeuralMutationGolden.ParentA); var b = new NeuralGenome(NeuralMutationGolden.ParentB);
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(a), NeuralMutationFixture.Parent(b, id: 9, x: 60)],
            NeuralMutationFixture.Settings(organisms: 2, cap: 3));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) reference.NextUInt();
        var expected = NeuralMutationFixture.MutatedWeights(a, 0.125, -1, 1, reference);
        simulation.AdvanceTick(); Assert.Equal(3, simulation.State.World.Organisms.Count);
        NeuralMutationFixture.AssertBits(expected, simulation.State.World.Organisms[2].Brain);
        Assert.Same(b, simulation.State.World.Organisms[1].Brain); Assert.Equal(160, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(160, 1)]
    public void IdExhaustionDoesNotAffectSkippedActiveMutationBirths(double energy, int cap)
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(energy: energy)],
            NeuralMutationFixture.Settings(cap: cap), nextOrganismId: int.MaxValue + 1L);
        var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Equal(random, simulation.RandomState); Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId);
        Assert.Equal(0, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void CombinedGoldenVerifiesAllScalarAndNeuralDrawsFromEachOwnParent()
    {
        var a = new NeuralGenome(NeuralMutationGolden.ParentA); var b = new NeuralGenome(NeuralMutationGolden.ParentB);
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(a),
            NeuralMutationFixture.Parent(b, id: 9, x: 60, heading: 1.2)],
            NeuralMutationFixture.Settings(organisms: 2, cap: 4, foodCap: 1, traitMutation: true));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 162; i++) reference.NextUInt();
        simulation.AdvanceTick(); var organisms = simulation.State.World.Organisms;
        Assert.Equal(new[] { 0, 9, 10, 11 }, organisms.Select(o => o.Id));
        NeuralMutationFixture.AssertEntity(NeuralMutationGolden.CombinedEntityA, organisms[2]);
        NeuralMutationFixture.AssertEntity(NeuralMutationGolden.CombinedEntityB, organisms[3]);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.CombinedChildA, organisms[2].Brain);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.CombinedChildB, organisms[3].Brain);
        Assert.Same(a, organisms[0].Brain); Assert.Same(b, organisms[1].Brain);
        Assert.NotSame(a, organisms[2].Brain); Assert.NotSame(b, organisms[3].Brain); Assert.NotSame(organisms[2].Brain, organisms[3].Brain);
        var food = Assert.Single(simulation.State.World.FoodItems);
        NeuralFixture.Near(NeuralMutationGolden.RegeneratedFood[0], food.X); NeuralFixture.Near(NeuralMutationGolden.RegeneratedFood[1], food.Y);
        Assert.Equal(new Pcg32State(NeuralMutationGolden.CombinedState, 3), simulation.RandomState);
        Assert.Equal(reference.CaptureState(), simulation.RandomState); Assert.Equal(168, NeuralMutationGolden.CombinedDraws);
    }

    [Fact]
    public void MultiGenerationGoldenDerivesGrandchildFromActualChildAndPreservesSnapshots()
    {
        var root = new NeuralGenome(NeuralMutationGolden.ParentA); var firstEntity = NeuralMutationGolden.GenerationOneEntity;
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(root, energy: 150)],
            NeuralMutationFixture.Settings(food: 1, pickup: 1e-8), [new Food(0, firstEntity[0], firstEntity[1], 110)]);
        var initial = simulation.State;
        simulation.AdvanceTick(); var firstSnapshot = simulation.State; var child = firstSnapshot.World.Organisms[1];
        NeuralMutationFixture.AssertEntity(firstEntity, child);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.GenerationOne, child.Brain);
        Assert.Equal(new Pcg32State(NeuralMutationGolden.GenerationOneState, 3), simulation.RandomState);
        Assert.Equal(50, child.Energy); Assert.Equal(100, firstSnapshot.World.Organisms[0].Energy);
        simulation.AdvanceTick(); var grandchild = simulation.State.World.Organisms[2];
        NeuralMutationFixture.AssertEntity(NeuralMutationGolden.GenerationTwoEntity, grandchild);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.GenerationTwo, grandchild.Brain);
        Assert.Equal(new Pcg32State(NeuralMutationGolden.GenerationTwoState, 3), simulation.RandomState);
        Assert.NotSame(root, child.Brain); Assert.NotSame(child.Brain, grandchild.Brain);
        Assert.Same(child.Brain, simulation.State.World.Organisms[1].Brain);
        Assert.Equal(150, initial.World.Organisms[0].Energy); Assert.Equal(50, child.Energy);
        Assert.Equal(110, simulation.State.World.Organisms[1].Energy); Assert.Equal(50, grandchild.Energy);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.ParentA, root);
        NeuralMutationFixture.AssertBits(NeuralMutationGolden.GenerationOne, child.Brain);
        Assert.Equal(2, simulation.GetStatistics().TotalBirths); Assert.Equal(165, NeuralMutationGolden.GenerationDraws);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.125)]
    [InlineData(double.MaxValue)]
    public void MutationAmplitudeDoesNotChangeInitialization(double mutation)
    {
        var settings = NeuralMutationFixture.Settings(mutation, initialAmplitude: 0.5);
        var baseline = NeuralMutationFixture.Settings(0, initialAmplitude: 0.5);
        var first = new Simulation(settings); var second = new Simulation(baseline);
        NeuralFixture.SameState(first, second);
    }

    [Fact]
    public void StatisticsOnlyObserveInheritedTraitsAndConsumeNoRngAfterMutation()
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent()]); simulation.AdvanceTick();
        var random = simulation.RandomState; var state = simulation.State; var first = simulation.GetStatistics();
        StatisticsFixture.AssertBits(first, simulation.GetStatistics()); Assert.Equal(random, simulation.RandomState); Assert.Same(state, simulation.State);
        Assert.Equal(2, first.OrganismCount); Assert.Equal(1, first.TotalBirths); Assert.Equal(0, first.TotalDeaths);
        Assert.Equal(0, first.Speed.Mean); Assert.Equal(0, first.Metabolism.Mean); Assert.Equal(30, first.SensingRadius.Mean);
    }

    [Fact]
    public void ActiveMutationReproducesCompleteStateFor5000Ticks()
    {
        var settings = NeuralMutationFixture.Settings(0.125, organisms: 2, food: 10, cap: 20, foodCap: 20,
            initialAmplitude: 0.5, duration: 0.001, interval: 0.01, pickup: 5, traitMutation: true);
        Simulation Create()
        {
            var initial = WorldInitializer.Create(settings);
            var parents = initial.Organisms.Select(o => new Organism(o.Id, o.X, o.Y, o.HeadingRadians, 100000,
                10, 0.001, o.SensingRadius, 2, o.Brain));
            return new Simulation(settings, new WorldState(100, 80, parents, initial.FoodItems));
        }
        var first = Create(); var second = Create();
        for (var i = 0; i < 5000; i++)
        {
            first.GetStatistics(); first.AdvanceTick(); second.AdvanceTick();
            if (i % 100 == 0) NeuralFixture.SameState(first, second);
        }
        Assert.Equal(5000, first.TickCount); Assert.True(first.GetStatistics().TotalBirths > 0);
        NeuralFixture.SameState(first, second);
    }
}
