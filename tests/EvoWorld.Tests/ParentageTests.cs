using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class ParentageTests
{
    internal static Organism Organism(int id, int? parent = null, double energy = 100, Species species = Species.Herbivore,
        double metabolism = 0, double x = 50, double speed = 0) =>
        new(id, x, 40, 0, energy, speed, metabolism, 0, 0, EvoWorld.Core.Organism.ZeroBrain, species, parent);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void ParentValidationAcceptsUnspecifiedAndArbitraryNonSelfIds(int? parent)
    {
        var organism = Organism(7, parent); Assert.Equal(parent, organism.ParentId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(7)]
    public void InvalidParentIdsNameTheAdditiveParameter(int parent) =>
        Assert.Equal("parentId", Assert.Throws<ArgumentOutOfRangeException>(() => Organism(7, parent)).ParamName);

    [Fact]
    public void HistoricalConstructorsAndInitializedFoundersHaveNoParent()
    {
        Assert.Null(new Organism(7, 1, 1, 0, 100).ParentId);
        var simulation = new Simulation(PredationFixture.Settings(neural: true, initialNeural: 0.5));
        Assert.All(simulation.State.World.Organisms, o => Assert.Null(o.ParentId));
    }

    [Theory]
    [InlineData(false, 0.0, 6)]
    [InlineData(true, 0.0, 6)]
    [InlineData(true, 0.1, 80)]
    public void FounderAndPreviouslyBornParentsProduceExactProvenanceWithoutNewRandomDraws(bool neural, double mutation, int draws)
    {
        var simulation = PredationFixture.Create([Organism(7, energy: 300), Organism(43, parent: 999, energy: 300, species: Species.Predator)],
            neural: neural, radius: 0, cap: 6, mutation: mutation);
        var random = new Pcg32(simulation.RandomState);
        simulation.AdvanceTick();
        for (var i = 0; i < 2 * draws; i++) random.NextUInt();
        Assert.Equal(random.CaptureState(), simulation.RandomState);
        var organisms = simulation.State.World.Organisms;
        Assert.Equal(new int?[] { null, 999, 7, 43 }, organisms.Select(o => o.ParentId));
        simulation.AdvanceTick();
        for (var i = 0; i < 2 * draws; i++) random.NextUInt();
        Assert.Equal(random.CaptureState(), simulation.RandomState);
        Assert.Equal(new int?[] { null, 999, 7, 43, 7, 43 }, simulation.State.World.Organisms.Select(o => o.ParentId));
    }

    [Fact]
    public void RealMultiGenerationChainSurvivesParentRemoval()
    {
        var first = ReproductionFixture.Create([new Organism(7, 50, 40, 0, 1000)], cost: 200, childEnergy: 200, cap: 8);
        first.AdvanceTick();
        // Resume the real immutable birth snapshot under a lower-cost reproduction setting.
        var simulation = new Simulation(PredationFixture.Settings(herbivores: 2, predators: 0, radius: 0, cap: 8), first.State.World, 1);
        simulation.AdvanceTick();
        var child = simulation.State.World.Organisms.Single(o => o.Id == 8);
        var grandchild = simulation.State.World.Organisms.Single(o => o.ParentId == 8);
        Assert.Equal(7, child.ParentId); Assert.Equal(8, grandchild.ParentId);
        var orphanWorld = new Simulation(PredationFixture.Settings(herbivores: 1, predators: 0),
            new WorldState(100, 80, [grandchild], []));
        orphanWorld.AdvanceTick(); Assert.Equal(8, Assert.Single(orphanWorld.State.World.Organisms).ParentId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeedingReflectionAttackAndReproductionPreserveAdultParent(bool predator)
    {
        var actor = Organism(7, 999, 200, predator ? Species.Predator : Species.Herbivore, x: 99, speed: 2);
        var simulation = PredationFixture.Create(predator ? [actor, Organism(43, energy: 20, x: 99)] : [actor],
            food: [new Food(0, 99, 40, 20)], cap: 4, radius: 8);
        simulation.AdvanceTick();
        var adult = simulation.State.World.Organisms.Single(o => o.Id == 7);
        Assert.Equal(999, adult.ParentId); Assert.Equal(99, adult.X); Assert.Equal(Math.PI, adult.HeadingRadians);
        Assert.Contains(simulation.State.World.Organisms, o => o.ParentId == 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TentativeBirthFailuresPreserveWorldAndRetryGenealogy(int failure)
    {
        Simulation Create() => PredationFixture.Create([Organism(7, 999, 200)], neural: true, mutation: 0.1, cap: 2, radius: 0);
        var simulation = Create(); var reference = Create(); var before = simulation.State;
        var random = simulation.RandomState; var id = simulation.NextOrganismId;
        simulation.FailBeforeMutationForTesting = failure == 0;
        simulation.FailOffspringTraitConstructionForTesting = failure == 1;
        simulation.FailWorldConstructionForTesting = failure == 2;
        Assert.ThrowsAny<Exception>(simulation.AdvanceTick);
        Assert.Same(before, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Equal(id, simulation.NextOrganismId);
        Assert.Equal(999, Assert.Single(simulation.State.World.Organisms).ParentId);
        simulation.FailBeforeMutationForTesting = simulation.FailOffspringTraitConstructionForTesting = simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); reference.AdvanceTick(); PredationFixture.Same(reference, simulation);
        Assert.Equal(new int?[] { 999, 7 }, simulation.State.World.Organisms.Select(o => o.ParentId));
    }

    [Fact]
    public void NewbornIsPresentAtBirthCommitBeforeAnyDrainOrPredatorAction()
    {
        var simulation = PredationFixture.Create([Organism(7, energy: 200), Organism(43, species: Species.Predator, energy: 100, x: 90)], cap: 3);
        var before = simulation.State;
        simulation.AdvanceTick();
        var newborn = Assert.Single(simulation.State.World.Organisms, o => before.World.Organisms.All(p => p.Id != o.Id));
        Assert.Equal(7, newborn.ParentId); Assert.Equal(simulation.Settings.OffspringInitialEnergy, newborn.Energy);
        Assert.Equal(1, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void OffspringWhoseFirstDrainWillKillThemStillAppearInTheBirthSnapshot()
    {
        var simulation = PredationFixture.Create([Organism(7, energy: 1000, metabolism: 1)], duration: 100, radius: 0, cap: 2);
        simulation.AdvanceTick(); var newborn = simulation.State.World.Organisms.Single(o => o.ParentId == 7);
        Assert.Equal(50, newborn.Energy); Assert.True(newborn.Metabolism * simulation.Settings.TickDurationSeconds > newborn.Energy);
        // The parent also reproduces next tick, but the previous newborn must die only now.
        simulation.AdvanceTick(); Assert.DoesNotContain(simulation.State.World.Organisms, o => o.Id == newborn.Id);
        Assert.Equal(1, simulation.GetStatistics().TotalDeaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservabilityDoesNotChangeLongRunningCoreTransactions(bool neural)
    {
        var options = new ExperimentOptions(neural ? ControllerMode.Neural : ControllerMode.RuleBased, 0.1, 5);
        var experiment = new DesktopExperiment(); experiment.Select(options); experiment.Reset();
        var reference = new Simulation(ExperimentSettings.Create(options));
        experiment.Inspection.Select(experiment.Simulation, 0);
        reference.EnableDiagnosticsFor(0);
        for (var i = 0; i < 500; i++)
        {
            experiment.AdvanceTick(); reference.AdvanceTick(); PredationFixture.Same(reference, experiment.Simulation);
            Assert.Equal(reference.State.World.Organisms.Select(o => o.ParentId), experiment.Simulation.State.World.Organisms.Select(o => o.ParentId));
        }
    }
}
