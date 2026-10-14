using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorIdContinuityTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    public void FirstProductionBirthNeverReusesPredatorId(int herbivores, int predators)
    {
        var simulation = new Simulation(PredatorFixture.Settings(herbivores, predators, energy: 160, food: 0, foodCap: 0));
        var total = herbivores + predators;
        Assert.Equal(total, simulation.NextOrganismId); simulation.AdvanceTick();
        Assert.Equal(total + predators, simulation.NextOrganismId);
        Assert.Equal(Enumerable.Range(0, total + predators), simulation.State.World.Organisms.Select(o => o.Id));
        Assert.All(simulation.State.World.Organisms.Skip(total), o => Assert.Equal(Species.Predator, o.Species));
        Assert.Equal(predators, simulation.GetStatistics().TotalBirths);
    }

    [Theory]
    [InlineData(false, 0.0, 6)]
    [InlineData(false, 0.125, 6)]
    [InlineData(true, 0.0, 6)]
    [InlineData(true, 0.125, 80)]
    public void MixedParentsInheritTheirOwnSpeciesAndBrainWithUnchangedBirthDraws(bool neural, double mutation, int draws)
    {
        var settings = PredatorFixture.Settings(neural: neural, mutation: mutation, food: 0, foodCap: 0);
        var a = PredatorFixture.Organism(Species.Herbivore, id: 7, brain: NeuralFixture.Brain(0.3));
        var b = PredatorFixture.Organism(Species.Predator, id: 43, brain: NeuralFixture.Brain(-0.4));
        var simulation = PredatorFixture.Create(settings, a, b);
        var before = simulation.RandomState;
        var reference = new Pcg32(before);
        simulation.AdvanceTick();
        var organisms = simulation.State.World.Organisms;
        Assert.Equal(new[] { 7, 43, 44, 45 }, organisms.Select(o => o.Id));
        Assert.Equal(Species.Herbivore, organisms[2].Species); Assert.Equal(Species.Predator, organisms[3].Species);
        Assert.Same(a.Brain, organisms[0].Brain); Assert.Same(b.Brain, organisms[1].Brain);
        for (var parent = 0; parent < 2; parent++)
        {
            var brain = parent == 0 ? a.Brain : b.Brain;
            for (var i = 0; i < 6; i++) reference.NextUInt();
            if (neural && mutation > 0)
            {
                Assert.NotSame(brain, organisms[parent + 2].Brain);
                NeuralMutationFixture.AssertBits(NeuralMutationFixture.MutatedWeights(brain, mutation, -1, 1, reference), organisms[parent + 2].Brain);
            }
            else Assert.Same(brain, organisms[parent + 2].Brain);
        }
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
        var count = new Pcg32(before);
        for (var i = 0; i < 2 * draws; i++) count.NextUInt();
        Assert.Equal(count.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void InitialPopulationAboveCeilingBlocksBirthsUntilDeathsFreeCapacity()
    {
        var settings = PredatorFixture.Settings(herbivores: 1, predators: 2, cap: 2, food: 0, foodCap: 0, duration: 1);
        var simulation = PredatorFixture.Create(settings,
            PredatorFixture.Organism(Species.Herbivore, id: 0, energy: 0.5, metabolism: 1),
            PredatorFixture.Organism(Species.Predator, id: 1, energy: 1.5, metabolism: 1),
            PredatorFixture.Organism(Species.Predator, id: 2));
        var random = simulation.RandomState;
        simulation.AdvanceTick(); Assert.Equal(2, simulation.State.World.Organisms.Count);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(0, simulation.GetStatistics().TotalBirths);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 2, 3 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(Species.Predator, o.Species));
        Assert.Equal(1, simulation.GetStatistics().TotalBirths); Assert.Equal(2, simulation.GetStatistics().TotalDeaths);
    }

    [Fact]
    public void CustomWorldUsesCombinedConfiguredCountActualSparseIdsAndExplicitHistory()
    {
        var settings = PredatorFixture.Settings(herbivores: 1, predators: 3, food: 0, cap: 0);
        var empty = PredatorFixture.Create(settings);
        Assert.Equal(4, empty.NextOrganismId); Assert.Equal(0, empty.GetStatistics().TotalDeaths);
        var world = new WorldState(100, 80, [PredatorFixture.Organism(Species.Predator, id: 7), PredatorFixture.Organism(Species.Herbivore, id: int.MaxValue)], []);
        var simulation = new Simulation(settings, world);
        Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId); Assert.Equal(0, simulation.GetStatistics().TotalBirths);
        Assert.Equal(1, simulation.GetStatistics().PredatorCount); Assert.Equal(1, simulation.GetStatistics().HerbivoreCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Simulation(settings, world, nextOrganismId: int.MaxValue));
        var sparse = new WorldState(100, 80, [PredatorFixture.Organism(Species.Predator, id: 7)], []);
        var historical = new Simulation(settings, sparse, nextOrganismId: 100);
        Assert.Equal(100, historical.NextOrganismId); Assert.Equal(0, historical.GetStatistics().TotalDeaths);
    }

    [Fact]
    public void ProductionMixedPopulationMayStartAboveTheReproductionCeiling()
    {
        var simulation = new Simulation(PredatorFixture.Settings(herbivores: 2, predators: 3, energy: 160, cap: 2, food: 0, foodCap: 0));
        Assert.Equal(5, simulation.State.World.Organisms.Count);
        var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Equal(5, simulation.State.World.Organisms.Count); Assert.Equal(5, simulation.NextOrganismId);
        Assert.Equal(0, simulation.GetStatistics().TotalBirths); Assert.Equal(random, simulation.RandomState);
    }

    [Theory]
    [InlineData(Species.Herbivore)]
    [InlineData(Species.Predator)]
    public void FinalSharedIdCanBeAllocatedThenExhaustionRollsBack(Species species)
    {
        var settings = PredatorFixture.Settings(neural: true, food: 0, foodCap: 0);
        var simulation = PredatorFixture.Create(settings, PredatorFixture.Organism(species, id: int.MaxValue - 1, energy: 500));
        Assert.Equal(int.MaxValue, simulation.NextOrganismId);
        simulation.EnableDiagnosticsFor(int.MaxValue - 1); simulation.AdvanceTick();
        Assert.Equal(int.MaxValue, simulation.State.World.Organisms[1].Id);
        Assert.Equal(species, simulation.State.World.Organisms[1].Species);
        Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId);
        var sample = simulation.LastDiagnosticSample;
        NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(species, o.Species));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedMixedTickRollsBackSpeciesBrainsRngIdsAndDiagnosticSample(bool afterMutation)
    {
        var settings = PredatorFixture.Settings(neural: true, food: 0, duration: 0.2);
        Organism[] parents = [PredatorFixture.Organism(Species.Herbivore, id: 7, energy: 500), PredatorFixture.Organism(Species.Predator, id: 43, energy: 500)];
        var simulation = PredatorFixture.Create(settings, parents); var clean = PredatorFixture.Create(settings, parents);
        simulation.EnableDiagnosticsFor(43); clean.EnableDiagnosticsFor(43);
        simulation.AdvanceTick(); clean.AdvanceTick();
        var sample = simulation.LastDiagnosticSample; var species = simulation.State.World.Organisms.Select(o => o.Species).ToArray();
        simulation.FailOffspringTraitConstructionForTesting = afterMutation;
        simulation.FailWorldConstructionForTesting = !afterMutation;
        if (afterMutation) NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        else NeuralFixture.Rollback<ArgumentNullException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample); Assert.Equal(species, simulation.State.World.Organisms.Select(o => o.Species));
        simulation.FailOffspringTraitConstructionForTesting = simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        PredatorFixture.SameState(clean, simulation); PredatorFixture.SameSample(clean.LastDiagnosticSample, simulation.LastDiagnosticSample);
    }
}
