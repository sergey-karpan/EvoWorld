using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class DesktopPredatorDemonstrationTests
{
    public static IEnumerable<object[]> Selections()
    {
        foreach (var mode in new[] { 0, 1 })
            foreach (var mutation in new[] { 0.0, 0.05, 0.1, 0.5 })
                foreach (var predators in new[] { 0, 2, 5, 10 }) yield return [mode, mutation, predators];
    }

    [Theory]
    [MemberData(nameof(Selections))]
    public void AllFortyTwoSettingsMapExactlyAndOnlyThreeOverridesAreAllowed(int mode, double mutation, int predators)
    {
        var options = new ExperimentOptions((ControllerMode)mode, mutation, predators);
        var actual = ExperimentSettings.Create(options);
        Dictionary<string, object> expected = new()
        {
            ["Seed"] = 42UL, ["TickDurationSeconds"] = 1.0 / 30, ["WorldWidth"] = 1000.0, ["WorldHeight"] = 700.0,
            ["InitialOrganismCount"] = 20, ["InitialFoodCount"] = 100, ["OrganismSpeed"] = 50.0,
            ["OrganismEnergyDrainPerSecond"] = 1.0, ["FoodPickupRadius"] = 8.0,
            ["FoodRegenerationIntervalSeconds"] = 2.0, ["MaxFoodCount"] = 200,
            ["ReproductionEnergyThreshold"] = 150.0, ["ReproductionEnergyCost"] = 50.0, ["OffspringInitialEnergy"] = 50.0,
            ["MaxOrganismCount"] = 500, ["InitialSpeedVariationAmplitude"] = 0.0, ["InitialMetabolismVariationAmplitude"] = 0.0,
            ["MinSpeed"] = 0.0, ["MaxSpeed"] = 200.0, ["MinMetabolism"] = 0.0, ["MaxMetabolism"] = 5.0,
            ["SpeedMutationAmplitude"] = 1.0, ["MetabolismMutationAmplitude"] = 0.05,
            ["OrganismSensingRadius"] = 30.0, ["OrganismSteeringRate"] = 2.0,
            ["InitialSensingRadiusVariationAmplitude"] = 0.0, ["InitialSteeringRateVariationAmplitude"] = 0.0,
            ["MinSensingRadius"] = 0.0, ["MaxSensingRadius"] = 100.0, ["MinSteeringRate"] = 0.0, ["MaxSteeringRate"] = 5.0,
            ["SensingRadiusMutationAmplitude"] = 2.0, ["SteeringRateMutationAmplitude"] = 0.1,
            ["EnableNeuralControl"] = mode == 1, ["NeuralWeightMin"] = -1.0, ["NeuralWeightMax"] = 1.0,
            ["InitialNeuralWeightVariationAmplitude"] = 0.5, ["SpeedModulationAmplitude"] = 0.5,
            ["NeuralWeightMutationAmplitude"] = mode == 1 ? mutation : 0.0,
            ["InitialPredatorCount"] = predators, ["InitialPredatorEnergy"] = 100.0, ["PredationEnergyTransferRatio"] = 0.8
        };
        var properties = typeof(SimulationSettings).GetProperties();
        Assert.Equal(42, properties.Length); Assert.Equal(expected.Keys.Order(), properties.Select(p => p.Name).Order());
        var baseline = SimulationSettings.CreateDefault();
        foreach (var property in properties)
        {
            Assert.Equal(expected[property.Name], property.GetValue(actual));
            if (property.Name is not "EnableNeuralControl" and not "NeuralWeightMutationAmplitude" and not "InitialPredatorCount")
                Assert.Equal(property.GetValue(baseline), property.GetValue(actual));
        }
        Assert.Equal(predators, options.Effective.InitialPredatorCount);
        Assert.Equal(mode == 1 ? mutation : 0, options.Effective.MutationAmplitude);
        var simulation = new Simulation(actual); var counts = simulation.GetStatistics();
        Assert.Equal(20, counts.HerbivoreCount); Assert.Equal(predators, counts.PredatorCount);
        Assert.Equal(20 + predators, counts.OrganismCount); Assert.Equal(0, counts.TotalPredationEvents);
        Assert.All(simulation.State.World.Organisms.Where(o => o.Species == Species.Predator), o => Assert.Equal(100, o.Energy));
    }

    [Fact]
    public void InitialAndHistoricalOptionConstructorKeepZeroPredatorsAndStableChoices()
    {
        Assert.Equal(new ExperimentOptions(ControllerMode.RuleBased, 0.1, 0), ExperimentOptions.Initial);
        Assert.Equal(ExperimentOptions.Initial, new ExperimentOptions(ControllerMode.RuleBased, 0.1));
        Assert.Equal(new[] { 0, 2, 5, 10 }, ExperimentSettings.PredatorChoices.ToArray());
        Assert.Equal(new[] { 0.0, 0.05, 0.1, 0.5 }, ExperimentSettings.MutationChoices.ToArray());
        Assert.Equal(0.1, ExperimentOptions.Initial.MutationAmplitude); Assert.Equal(0, ExperimentOptions.Initial.Effective.MutationAmplitude);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void InvalidPredatorChoiceCannotChangePendingActiveWorldOrRng(int predators)
    {
        var experiment = new DesktopExperiment(); var simulation = experiment.Simulation;
        var state = simulation.State; var random = simulation.RandomState;
        Assert.Throws<ArgumentOutOfRangeException>(() => experiment.Select(new(ControllerMode.Neural, 0.1, predators)));
        Assert.Equal(ExperimentOptions.Initial, experiment.PendingOptions); Assert.Same(simulation, experiment.Simulation);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void InvalidControllerAndMutationAreStillRejectedWithValidPredatorCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentSettings.Create(new((ControllerMode)99, 0.1, 5)));
        foreach (var mutation in new[] { double.NaN, double.PositiveInfinity, -1, 0.2 })
            Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentSettings.Create(new(ControllerMode.RuleBased, mutation, 5)));
    }

    [Fact]
    public void PredatorOnlyAndCombinedPendingChangesComposeWithoutChangingActiveInspection()
    {
        var experiment = new DesktopExperiment(); var simulation = experiment.Simulation;
        experiment.Inspection.Select(simulation, 0); experiment.AdvanceTick();
        var state = simulation.State; var random = simulation.RandomState; var stats = simulation.GetStatistics();
        var sample = simulation.LastDiagnosticSample; var trail = experiment.Inspection[1];
        experiment.Select(new(ControllerMode.RuleBased, 0.5, 2)); Assert.True(experiment.HasPendingChanges);
        Assert.Equal(0.5, experiment.PendingOptions.MutationAmplitude); Assert.Equal(0, experiment.PendingOptions.Effective.MutationAmplitude);
        experiment.Select(new(ControllerMode.Neural, 0.05, 5)); Assert.True(experiment.HasPendingChanges);
        Assert.Same(simulation, experiment.Simulation); Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
        Assert.Equal(stats, simulation.GetStatistics()); Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.Equal(0, experiment.Inspection.SelectedId); Assert.Equal(trail, experiment.Inspection[1]);
        experiment.Select(new(ControllerMode.RuleBased, 0.5, 0)); Assert.False(experiment.HasPendingChanges);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 5)]
    [InlineData(1, 10)]
    public void SuccessfulResetAppliesCompleteSelectionAndClearsAllInspection(int mode, int predators)
    {
        var experiment = new DesktopExperiment(); var previous = experiment.Simulation;
        experiment.Inspection.Select(previous, 0); experiment.AdvanceTick();
        experiment.Select(new((ControllerMode)mode, 0.05, predators)); experiment.Reset();
        var run = experiment.Simulation; Assert.NotSame(previous, run); Assert.Equal(0, run.TickCount);
        Assert.Equal(new ExperimentOptions((ControllerMode)mode, mode == 1 ? 0.05 : 0, predators), experiment.ActiveOptions);
        Assert.Equal(mode == 1, run.Settings.EnableNeuralControl); Assert.Equal(predators, run.Settings.InitialPredatorCount);
        Assert.False(experiment.HasPendingChanges); Assert.Null(experiment.Inspection.SelectedId); Assert.Equal(0, experiment.Inspection.Count);
        Assert.Null(experiment.Inspection.LastAlive); Assert.Null(experiment.Inspection.LastActed);
        Assert.Null(previous.DiagnosticOrganismId); Assert.Null(run.DiagnosticOrganismId); Assert.Null(run.LastDiagnosticSample);
        var stats = run.GetStatistics(); Assert.Equal(20, stats.HerbivoreCount); Assert.Equal(predators, stats.PredatorCount);
        Assert.Equal(0, stats.TotalBirths); Assert.Equal(0, stats.TotalDeaths); Assert.Equal(0, stats.TotalPredationEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedResetPreservesLiveOrFrozenPredatorInspectionAndRetryAppliesPending(bool deceased)
    {
        var fail = false; var calls = 0;
        var experiment = new DesktopExperiment(settings =>
        {
            if (fail) throw new InvalidOperationException("Rejected reset");
            if (calls++ > 0) return new Simulation(settings);
            var predator = new Organism(7, 500, 350, 0, 0.05, 0, 1, 30, 0, Organism.ZeroBrain, Species.Predator);
            return new Simulation(settings, new WorldState(1000, 700, [predator], []));
        });
        var previous = experiment.Simulation; experiment.Inspection.Select(previous, 7); experiment.AdvanceTick();
        if (deceased) experiment.AdvanceTick();
        var state = previous.State; var random = previous.RandomState; var sample = previous.LastDiagnosticSample;
        var alive = experiment.Inspection.LastAlive; var acted = experiment.Inspection.LastActed;
        var trace = Enumerable.Range(0, experiment.Inspection.Count).Select(i => experiment.Inspection[i]).ToArray();
        var active = experiment.ActiveOptions; var choice = new ExperimentOptions(ControllerMode.Neural, 0.05, 5);
        experiment.Select(choice); fail = true;
        Assert.Throws<InvalidOperationException>(experiment.Reset);
        Assert.Same(previous, experiment.Simulation); Assert.Same(state, previous.State); Assert.Equal(random, previous.RandomState);
        Assert.Same(sample, previous.LastDiagnosticSample); Assert.Equal(7, previous.DiagnosticOrganismId);
        Assert.Same(alive, experiment.Inspection.LastAlive); Assert.Same(acted, experiment.Inspection.LastActed);
        Assert.Equal(trace, Enumerable.Range(0, experiment.Inspection.Count).Select(i => experiment.Inspection[i]));
        Assert.Equal(!deceased, experiment.Inspection.IsAlive); Assert.Equal(choice, experiment.PendingOptions);
        Assert.Equal(active, experiment.ActiveOptions); Assert.True(experiment.HasPendingChanges);
        fail = false; experiment.Reset(); Assert.Equal(choice.Effective, experiment.ActiveOptions);
        Assert.False(experiment.HasPendingChanges); Assert.Equal(5, experiment.Simulation.GetStatistics().PredatorCount);
        Assert.Null(experiment.Inspection.SelectedId); Assert.Equal(0, experiment.Inspection.Count); Assert.Equal(0, experiment.Simulation.TickCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroPredatorMappingMatchesHistoricalConstructorCompleteWorldAndRng(bool neural)
    {
        var historical = new Simulation(new SimulationSettings(42, 1.0 / 30, 1000, 700, 20, 100, 50, 1, 8, 2, 200,
            150, 50, 50, 500, 0, 0, 0, 200, 0, 5, 1, 0.05, 30, 2, 0, 0, 0, 100, 0, 5, 2, 0.1,
            neural, -1, 1, 0.5, 0.5, neural ? 0.1 : 0));
        var current = new Simulation(ExperimentSettings.Create(new(neural ? ControllerMode.Neural : ControllerMode.RuleBased, 0.1, 0)));
        for (var tick = 0; tick <= 150; tick++)
        {
            PredatorFixture.SameState(historical, current);
            if (tick < 150) { historical.AdvanceTick(); current.AdvanceTick(); }
        }
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0, 5)]
    [InlineData(0, 10)]
    [InlineData(1, 2)]
    [InlineData(1, 5)]
    [InlineData(1, 10)]
    public void RepeatedResetReproducesMixedWorldAndTrajectoryWithConsistentLiveStatistics(int mode, int predators)
    {
        var experiment = new DesktopExperiment(); var options = new ExperimentOptions((ControllerMode)mode, 0.1, predators);
        experiment.Select(options); experiment.Reset(); var first = experiment.Simulation;
        experiment.Reset(); var second = experiment.Simulation; Assert.NotSame(first, second);
        long predations = 0;
        for (var tick = 0; tick <= 500; tick++)
        {
            PredatorFixture.SameState(first, second); var stats = second.GetStatistics(out var snapshot);
            Assert.Equal(snapshot.World.Organisms.Count, stats.OrganismCount);
            Assert.Equal(stats.OrganismCount, stats.HerbivoreCount + stats.PredatorCount);
            Assert.True(stats.TotalPredationEvents >= predations); predations = stats.TotalPredationEvents;
            Assert.Equal(20L + predators + stats.TotalBirths - stats.OrganismCount, stats.TotalDeaths);
            Assert.Equal(predators, experiment.ActiveOptions.InitialPredatorCount);
            if (tick < 500) { first.AdvanceTick(); experiment.AdvanceTick(); }
        }
    }

    [Theory]
    [InlineData(Species.Herbivore)]
    [InlineData(Species.Predator)]
    public void HitTestingAndInspectionRemainSpeciesIndependentWithoutConsumingRng(Species species)
    {
        var organism = new Organism(43, 30, 20, 0, 100, 0, 0, 0, 0, Organism.ZeroBrain, species);
        var simulation = new Simulation(ExperimentSettings.Create(ExperimentOptions.Initial), new WorldState(1000, 700, [organism], []));
        Assert.True(ViewportTransform.TryCreate(1000, 700, 800, 400, out var view));
        var x = view.OffsetX + 30 * view.Scale; var y = view.OffsetY + 20 * view.Scale;
        var state = simulation.State; var random = simulation.RandomState;
        Assert.Equal(43, OrganismHitTesting.Find(state.World, 800, 400, x / 2, y / 2, 2, 2));
        var inspection = new OrganismInspection(); inspection.Select(simulation, 43); inspection.Observe(simulation); inspection.Format(simulation);
        Assert.Equal(43, inspection.SelectedId); Assert.Equal(1, inspection.Count);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void ControlledConsumptionIsProducedOnlyByCoreAndReadOnlyUiEventsDoNotChangeCounters()
    {
        var settings = ExperimentSettings.Create(new(ControllerMode.RuleBased, 0.1, 2));
        var predator = new Organism(7, 500, 350, 0, 100, 0, 0, 0, 0, Organism.ZeroBrain, Species.Predator);
        var prey = new Organism(43, 500, 350, 0, 75, 0, 0, 0, 0, Organism.ZeroBrain, Species.Herbivore);
        var experiment = new DesktopExperiment(_ => new Simulation(settings, new WorldState(1000, 700, [predator, prey], [])));
        var simulation = experiment.Simulation; var random = simulation.RandomState;
        experiment.Inspection.Select(simulation, 7);
        experiment.Select(new(ControllerMode.Neural, 0.5, 10)); experiment.Inspection.Format(simulation);
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(0, simulation.TickCount);
        experiment.AdvanceTick(); var stats = simulation.GetStatistics();
        Assert.Equal(1, stats.TotalPredationEvents); Assert.Equal(1, stats.TotalDeaths); Assert.Equal(1, stats.TotalBirths);
        Assert.Equal(2, stats.PredatorCount); Assert.Equal(0, stats.HerbivoreCount);
        var reference = new EvoWorld.Core.Random.Pcg32(random); for (var i = 0; i < 6; i++) reference.NextUInt();
        Assert.Equal(reference.CaptureState(), simulation.RandomState); // Only the existing successful birth draws.
        Assert.Equal(43, simulation.LastDiagnosticSample!.PreyConsumedId);
        Assert.Equal(60, simulation.LastDiagnosticSample.EnergyGainedFromPrey);
    }

    [Fact]
    public void EmptyPopulationPreservesHistoricalPredationCountsAndZeroSpeciesTotals()
    {
        var settings = ExperimentSettings.Create(new(ControllerMode.RuleBased, 0.1, 2));
        var empty = new Simulation(settings, new WorldState(1000, 700, [], []), 20, null, null, 3);
        var experiment = new DesktopExperiment(_ => empty); experiment.AdvanceTick();
        var stats = empty.GetStatistics(out var snapshot);
        Assert.Equal(snapshot.TickCount, stats.TickCount); Assert.Equal(0, stats.OrganismCount);
        Assert.Equal(0, stats.HerbivoreCount); Assert.Equal(0, stats.PredatorCount); Assert.Equal(3, stats.TotalPredationEvents);
        Assert.Equal(0, stats.TotalBirths); Assert.Equal(0, stats.TotalDeaths);
    }
}
