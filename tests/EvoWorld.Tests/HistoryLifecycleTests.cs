using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class HistoryLifecycleTests
{
    [Fact]
    public void PendingSelectionsAndInspectionLeaveHistoryAndFrozenMetadataUntouched()
    {
        var experiment = new DesktopExperiment(); var history = experiment.History; var metadata = history.Metadata;
        experiment.Select(new(ControllerMode.Neural, 0.5, 10)); experiment.Inspection.Select(experiment.Simulation, 0);
        experiment.Inspection.Format(experiment.Simulation); experiment.Inspection.Observe(experiment.Simulation);
        Assert.Same(history, experiment.History); Assert.Same(metadata, history.Metadata); Assert.Single(history);
        Assert.Equal(42UL, metadata.Seed); Assert.Equal(ControllerMode.RuleBased, metadata.Controller);
        Assert.False(metadata.NeuralGenomeActive); Assert.Equal(0, metadata.InitialPredatorCount); Assert.Equal(0, metadata.NeuralMutationAmplitude);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureDuringSimulationOrTickZeroHistoryConstructionPreservesExactActiveRunAndRetry(bool historyFails)
    {
        var fail = false;
        var experiment = new DesktopExperiment(
            settings => fail && !historyFails ? throw new InvalidOperationException("Simulation failed") : new Simulation(settings),
            simulation => fail && historyFails ? throw new InvalidOperationException("History failed") : new PopulationHistory(simulation));
        experiment.Inspection.Select(experiment.Simulation, 0);
        for (var i = 0; i < 200; i++) experiment.AdvanceTick();
        var simulation = experiment.Simulation; var history = experiment.History; var recorded = history.Capture();
        var state = simulation.State; var sample = simulation.LastDiagnosticSample; var random = simulation.RandomState;
        var trail = experiment.Inspection.Count; var active = experiment.ActiveOptions;
        experiment.Select(new(ControllerMode.Neural, 0.05, 5)); fail = true;
        Assert.Throws<InvalidOperationException>(experiment.Reset);
        Assert.Same(simulation, experiment.Simulation); Assert.Same(state, simulation.State); Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.Equal(random, simulation.RandomState); Assert.Same(history, experiment.History); Assert.Equal(recorded.Samples, history.Capture().Samples);
        Assert.Equal(trail, experiment.Inspection.Count); Assert.Equal(0, experiment.Inspection.SelectedId); Assert.Equal(active, experiment.ActiveOptions);
        Assert.True(experiment.HasPendingChanges);
        fail = false; experiment.Reset(); Assert.NotSame(history, experiment.History); Assert.Single(experiment.History);
        Assert.Equal(0, experiment.History[0].Tick); Assert.Equal(5, experiment.History.Metadata.InitialPredatorCount);
        Assert.Equal(ControllerMode.Neural, experiment.History.Metadata.Controller); Assert.Equal(0.05, experiment.History.Metadata.NeuralMutationAmplitude);
        Assert.Null(experiment.Inspection.SelectedId); Assert.Equal(0, experiment.Inspection.Count); Assert.False(experiment.HasPendingChanges);
    }

    [Fact]
    public void FailedCoreTickAtScheduledBoundaryDoesNotPublishHistory()
    {
        var experiment = new DesktopExperiment(); for (var i = 0; i < 99; i++) experiment.AdvanceTick();
        var history = experiment.History; var state = experiment.Simulation.State;
        experiment.Simulation.FailWorldConstructionForTesting = true;
        Assert.Throws<ArgumentNullException>(experiment.AdvanceTick);
        Assert.Same(state, experiment.Simulation.State); Assert.Single(history); Assert.Equal(1, history.Revision);
        experiment.Simulation.FailWorldConstructionForTesting = false; experiment.AdvanceTick();
        Assert.Equal(100, history[1].Tick); Assert.Null(experiment.HistoryObservationError);
    }

    [Fact]
    public void ObservationFailureDoesNotUndoCoreTickStopAdvancementOrPublishPartialSample()
    {
        // An internally injected high initial predation counter makes a later normal
        // committed counter violate the observer's monotonic invariant. Core is unchanged.
        var experiment = new DesktopExperiment(null, _ => new PopulationHistory(PopulationHistoryTests.Empty(predations: 5)));
        var state = experiment.Simulation.State; for (var i = 0; i < 100; i++) experiment.AdvanceTick();
        Assert.NotSame(state, experiment.Simulation.State); Assert.Equal(100, experiment.Simulation.TickCount);
        Assert.Single(experiment.History); Assert.Equal(5, experiment.History[0].TotalPredationEvents);
        Assert.Contains("History observation failed at tick 100", experiment.HistoryObservationError);
        experiment.AdvanceTick(); Assert.Equal(101, experiment.Simulation.TickCount); Assert.Single(experiment.History);
        Assert.NotNull(experiment.HistoryObservationError); // No silent clearing on an off-boundary tick.
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(1, 5)]
    public void ReproducibleRunsProduceIdenticalWorldHistoryAndCsvWithoutObserverRng(int mode, int predators)
    {
        var experiment = new DesktopExperiment(); experiment.Select(new((ControllerMode)mode, 0.1, predators)); experiment.Reset();
        var reference = new Simulation(ExperimentSettings.Create(experiment.PendingOptions));
        for (var i = 0; i < 1000; i++) { experiment.AdvanceTick(); reference.AdvanceTick(); PredatorFixture.SameState(reference, experiment.Simulation); }
        var recorded = experiment.History.Capture(); experiment.Reset();
        for (var i = 0; i < 1000; i++) experiment.AdvanceTick();
        Assert.Equal(recorded.Metadata, experiment.History.Metadata); Assert.Equal(recorded.Samples, experiment.History.Capture().Samples);
        using var first = new StringWriter(); using var second = new StringWriter();
        PopulationHistoryCsv.Write(first, recorded); PopulationHistoryCsv.Write(second, experiment.History.Capture());
        Assert.Equal(first.ToString(), second.ToString());
    }
}
