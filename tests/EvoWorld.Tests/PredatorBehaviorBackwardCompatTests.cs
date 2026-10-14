using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorBehaviorBackwardCompatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitZeroPredatorsMatchesHistoricalThirtyNineArgumentRunAndDiagnostics(bool neural)
    {
        var oldSettings = new SimulationSettings(42, 0.1, 100, 80, 2, 1, 10, 1, 8, 0.2, 100,
            150, 50, 50, 12, 10, 0.5, 0, 200, 0, 5, 1, 0.05, 30, 2, 10, 0.5, 0, 100, 0, 5, 2, 0.1,
            neural, -1, 1, 0.5, 0.5, 0.125);
        var old = new Simulation(oldSettings);
        var current = new Simulation(PredatorFixture.Settings(herbivores: 2, predators: 0, neural: neural, oldVariation: true, newVariation: true));
        old.EnableDiagnosticsFor(1); current.EnableDiagnosticsFor(1);
        for (var tick = 0; tick <= 500; tick++)
        {
            PredatorFixture.SameState(old, current); PredatorFixture.SameSample(old.LastDiagnosticSample, current.LastDiagnosticSample);
            Assert.All(current.State.World.Organisms, o => Assert.Equal(Species.Herbivore, o.Species));
            if (tick < 500) { old.AdvanceTick(); current.AdvanceTick(); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpeciesClassificationDoesNotAffectAnyTickBehaviorOrDiagnosticCommands(bool neural)
    {
        var settings = PredatorFixture.Settings(neural: neural, food: 0, duration: 0.2);
        var brain = NeuralFixture.Brain(0.3, -0.2);
        var herbivore = PredatorFixture.Create(settings, PredatorFixture.Organism(Species.Herbivore, energy: 500, speed: 10, metabolism: 1, brain: brain));
        var predator = PredatorFixture.Create(settings, PredatorFixture.Organism(Species.Predator, energy: 500, speed: 10, metabolism: 1, brain: brain));
        herbivore.EnableDiagnosticsFor(0); predator.EnableDiagnosticsFor(0);
        for (var tick = 1; tick <= 600; tick++)
        {
            herbivore.AdvanceTick(); predator.AdvanceTick();
            // Original helper compares every pre-species field and statistic, genomes,
            // RNG and IDs; the expected classification/count distinction is explicit.
            NeuralFixture.SameState(herbivore, predator);
            PredatorFixture.SameSample(herbivore.LastDiagnosticSample, predator.LastDiagnosticSample);
            Assert.All(herbivore.State.World.Organisms, o => Assert.Equal(Species.Herbivore, o.Species));
            Assert.All(predator.State.World.Organisms, o => Assert.Equal(Species.Predator, o.Species));
            Assert.Equal(herbivore.GetStatistics().OrganismCount, herbivore.GetStatistics().HerbivoreCount);
            Assert.Equal(predator.GetStatistics().OrganismCount, predator.GetStatistics().PredatorCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedSpeciesRunsReproduceCompleteStateOverFiveThousandTicks(bool neural)
    {
        var settings = PredatorFixture.Settings(herbivores: 2, predators: 2, neural: neural, energy: 200,
            oldVariation: true, newVariation: true, foodCap: 30);
        var first = new Simulation(settings); var second = new Simulation(settings);
        first.EnableDiagnosticsFor(2); second.EnableDiagnosticsFor(2);
        for (var tick = 0; tick <= 5000; tick++)
        {
            PredatorFixture.SameState(first, second); PredatorFixture.SameSample(first.LastDiagnosticSample, second.LastDiagnosticSample);
            if (tick > 0) { Assert.Equal(tick, first.LastDiagnosticSample!.TickCount); Assert.Equal(2, first.LastDiagnosticSample.OrganismId); }
            if (tick < 5000) { first.AdvanceTick(); second.AdvanceTick(); }
        }
    }

    [Fact]
    public void DiagnosticPublicShapeAndLifetimeAreUnchangedForPredators()
    {
        string[] expected = ["OrganismId", "TickCount", "DidAct", "IsNeuralMode", "IsRuleBasedMode",
            "NeuralOutputSteering", "NeuralOutputSpeed", "AppliedRotationRadians", "EffectiveSpeed"];
        Assert.Equal(expected.Order(), typeof(OrganismDiagnosticSample).GetProperties().Select(p => p.Name).Order());
        var simulation = PredatorFixture.Create(PredatorFixture.Settings(neural: true, food: 0, foodCap: 0, duration: 1),
            PredatorFixture.Organism(Species.Predator, id: 43, energy: 1.5, metabolism: 1));
        var state = simulation.State; var random = simulation.RandomState;
        simulation.EnableDiagnosticsFor(43); simulation.EnableDiagnosticsFor(43);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Null(simulation.LastDiagnosticSample);
        simulation.AdvanceTick(); var sample = simulation.LastDiagnosticSample!;
        Assert.True(sample.DidAct); Assert.Equal(43, sample.OrganismId); Assert.Equal(1, sample.TickCount);
        Assert.Equal(Math.Tanh(0.2), sample.NeuralOutputSteering); Assert.Equal(Math.Tanh(-0.3), sample.NeuralOutputSpeed);
        simulation.EnableDiagnosticsFor(43); Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.AdvanceTick(); Assert.False(simulation.LastDiagnosticSample!.DidAct);
        Assert.Equal(2, simulation.LastDiagnosticSample.TickCount); Assert.Null(simulation.LastDiagnosticSample.EffectiveSpeed);
        random = simulation.RandomState; state = simulation.State;
        simulation.DisableDiagnostics(); Assert.Null(simulation.LastDiagnosticSample);
        Assert.Equal(random, simulation.RandomState); Assert.Same(state, simulation.State);
    }

    [Fact]
    public void DesktopDefaultsPendingChoicesAndInspectionRemainHerbivoreOnly()
    {
        var experiment = new DesktopExperiment(); var simulation = experiment.Simulation;
        Assert.Equal(0, simulation.Settings.InitialPredatorCount); Assert.Equal(100, simulation.Settings.InitialPredatorEnergy);
        experiment.Inspection.Select(simulation, 0); experiment.AdvanceTick();
        var sample = simulation.LastDiagnosticSample;
        experiment.Select(new(ControllerMode.Neural, 0.1));
        Assert.Same(simulation, experiment.Simulation); Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.Equal(2, experiment.Inspection.Count);
        experiment.Reset();
        Assert.Equal(0, experiment.Simulation.GetStatistics().PredatorCount); Assert.Null(experiment.Inspection.SelectedId);
        Assert.All(experiment.Simulation.State.World.Organisms, o => Assert.Equal(Species.Herbivore, o.Species));
    }
}
