using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public class DesktopExperimentTests
{
    [Fact]
    public void InitialActiveConfigurationIsRuleBasedWithoutPendingChanges()
    {
        var experiment = new DesktopExperiment();
        Assert.Equal(new ExperimentOptions(ControllerMode.RuleBased, 0), experiment.ActiveOptions);
        Assert.Equal(ExperimentOptions.Initial, experiment.PendingOptions);
        Assert.False(experiment.HasPendingChanges); Assert.False(experiment.Simulation.Settings.EnableNeuralControl);
        Assert.Equal(0, experiment.Simulation.TickCount);
    }

    [Fact]
    public void SelectionDoesNotConstructAdvanceOrModifyActiveSimulation()
    {
        var calls = 0;
        var experiment = new DesktopExperiment(settings => { calls++; return new Simulation(settings); });
        var simulation = experiment.Simulation; simulation.AdvanceTick(); var state = simulation.State;
        var random = simulation.RandomState; var brain = state.World.Organisms[0].Brain;
        experiment.Select(new(ControllerMode.Neural, 0.1));
        Assert.Same(simulation, experiment.Simulation); Assert.Same(state, experiment.Simulation.State);
        Assert.Equal(1, experiment.Simulation.TickCount); Assert.Equal(random, experiment.Simulation.RandomState);
        Assert.Same(brain, experiment.Simulation.State.World.Organisms[0].Brain); Assert.Equal(1, calls);
        Assert.Equal(ControllerMode.RuleBased, experiment.ActiveOptions.Controller); Assert.True(experiment.HasPendingChanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.05)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    public void SuccessfulResetActivatesSelectedNeuralConfigurationAndNewInstance(double amplitude)
    {
        var experiment = new DesktopExperiment(); var previous = experiment.Simulation;
        previous.AdvanceTick(); var previousState = previous.State; var previousRandom = previous.RandomState;
        experiment.Select(new(ControllerMode.Neural, amplitude)); Assert.True(experiment.HasPendingChanges);
        experiment.Reset();
        Assert.NotSame(previous, experiment.Simulation); Assert.Equal(0, experiment.Simulation.TickCount);
        Assert.True(experiment.Simulation.Settings.EnableNeuralControl);
        Assert.Equal(amplitude, experiment.Simulation.Settings.NeuralWeightMutationAmplitude);
        Assert.True(experiment.Simulation.Settings.InitialNeuralWeightVariationAmplitude > 0);
        Assert.False(experiment.HasPendingChanges); Assert.Equal(new ExperimentOptions(ControllerMode.Neural, amplitude), experiment.ActiveOptions);
        Assert.Same(previousState, previous.State); Assert.Equal(previousRandom, previous.RandomState); Assert.Equal(1, previous.TickCount);
    }

    [Fact]
    public void ReturningToRuleBasedDisablesNeuralControlRegardlessOfLastAmplitude()
    {
        var experiment = new DesktopExperiment(); experiment.Select(new(ControllerMode.Neural, 0.5)); experiment.Reset();
        experiment.Select(new(ControllerMode.RuleBased, 0.5)); Assert.True(experiment.HasPendingChanges);
        experiment.Reset(); Assert.False(experiment.Simulation.Settings.EnableNeuralControl);
        Assert.Equal(0, experiment.Simulation.Settings.NeuralWeightMutationAmplitude); Assert.False(experiment.HasPendingChanges);
        Assert.Equal(0.5, experiment.PendingOptions.MutationAmplitude);
        NeuralFixture.SameState(new Simulation(SimulationSettings.CreateDefault()), experiment.Simulation);
    }

    [Fact]
    public void PendingIndicationReflectsEffectiveConfigurationIncludingUndoAndInactiveAmplitude()
    {
        var experiment = new DesktopExperiment(); experiment.Select(new(ControllerMode.RuleBased, 0.5));
        Assert.False(experiment.HasPendingChanges);
        experiment.Select(new(ControllerMode.Neural, 0.5)); Assert.True(experiment.HasPendingChanges);
        experiment.Select(new(ControllerMode.RuleBased, 0.5)); Assert.False(experiment.HasPendingChanges);
        experiment.Select(new(ControllerMode.Neural, 0.1)); experiment.Reset();
        experiment.Select(new(ControllerMode.Neural, 0.5)); Assert.True(experiment.HasPendingChanges);
        experiment.Select(new(ControllerMode.Neural, 0.1)); Assert.False(experiment.HasPendingChanges);
    }

    [Fact]
    public void AdvancingActiveSimulationWithPendingChoicesDoesNotApplyThem()
    {
        // Start/Pause are timer operations in WPF. The owner provides only its
        // active simulation, so advancing/resuming it cannot activate selections.
        var experiment = new DesktopExperiment(); var previous = experiment.Simulation;
        experiment.Select(new(ControllerMode.Neural, 0.1)); var reference = new Simulation(SimulationSettings.CreateDefault());
        for (var i = 0; i < 100; i++)
        {
            experiment.Simulation.AdvanceTick(); reference.AdvanceTick(); NeuralFixture.SameState(reference, experiment.Simulation);
        }
        Assert.Same(previous, experiment.Simulation); Assert.True(experiment.HasPendingChanges);
        Assert.Equal(ControllerMode.RuleBased, experiment.ActiveOptions.Controller);
    }

    [Fact]
    public void ResetConstructionFailurePreservesActiveStatePendingChoicesAndRetry()
    {
        var fail = false;
        var experiment = new DesktopExperiment(settings => fail ? throw new InvalidOperationException("Construction failed") : new Simulation(settings));
        var previous = experiment.Simulation; previous.AdvanceTick(); var state = previous.State; var random = previous.RandomState;
        experiment.Select(new(ControllerMode.Neural, 0.1)); fail = true;
        Assert.Equal("Construction failed", Assert.Throws<InvalidOperationException>(experiment.Reset).Message);
        Assert.Same(previous, experiment.Simulation); Assert.Same(state, experiment.Simulation.State);
        Assert.Equal(random, experiment.Simulation.RandomState); Assert.Equal(1, experiment.Simulation.TickCount);
        Assert.Equal(new ExperimentOptions(ControllerMode.Neural, 0.1), experiment.PendingOptions);
        Assert.Equal(ControllerMode.RuleBased, experiment.ActiveOptions.Controller); Assert.True(experiment.HasPendingChanges);
        experiment.Select(new(ControllerMode.Neural, 0.05)); fail = false; experiment.Reset();
        Assert.True(experiment.Simulation.Settings.EnableNeuralControl); Assert.Equal(0.05, experiment.Simulation.Settings.NeuralWeightMutationAmplitude);
        Assert.False(experiment.HasPendingChanges); Assert.Same(state, previous.State);
    }

    [Fact]
    public void NullFactoryResultCannotReplaceActiveSimulation()
    {
        var fail = false;
        var experiment = new DesktopExperiment(settings => fail ? null! : new Simulation(settings));
        var previous = experiment.Simulation; experiment.Select(new(ControllerMode.Neural, 0.1)); fail = true;
        Assert.Throws<InvalidOperationException>(experiment.Reset);
        Assert.Same(previous, experiment.Simulation); Assert.True(experiment.HasPendingChanges);
    }

    [Theory]
    [MemberData(nameof(ExperimentSettingsTests.AllSelections), MemberType = typeof(ExperimentSettingsTests))]
    public void ResetWithSameOptionsAlwaysRecreatesAndReproducesCompleteHistory(int mode, double amplitude)
    {
        var experiment = new DesktopExperiment(); experiment.Select(new((ControllerMode)mode, amplitude)); experiment.Reset();
        var previous = experiment.Simulation; var initial = previous.State;
        var twin = new DesktopExperiment(); twin.Select(new((ControllerMode)mode, amplitude)); twin.Reset();
        for (var i = 0; i < 100; i++) { previous.AdvanceTick(); twin.Simulation.AdvanceTick(); }
        NeuralFixture.SameState(previous, twin.Simulation);
        experiment.Reset(); Assert.NotSame(previous, experiment.Simulation); Assert.Equal(0, experiment.Simulation.TickCount);
        Assert.NotSame(initial, experiment.Simulation.State); Assert.False(experiment.HasPendingChanges);
        twin.Reset(); NeuralFixture.SameState(twin.Simulation, experiment.Simulation);
        Assert.Equal(initial.World.Organisms.Select(o => (o.X, o.Y, o.HeadingRadians)),
            experiment.Simulation.State.World.Organisms.Select(o => (o.X, o.Y, o.HeadingRadians)));
        Assert.Equal(100, previous.TickCount);
    }

    [Fact]
    public void InvalidSelectionCannotChangePendingOrActiveChoices()
    {
        var experiment = new DesktopExperiment(); var simulation = experiment.Simulation;
        Assert.Throws<ArgumentOutOfRangeException>(() => experiment.Select(new(ControllerMode.Neural, 0.2)));
        Assert.Equal(ExperimentOptions.Initial, experiment.PendingOptions); Assert.Same(simulation, experiment.Simulation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    public void SelectedMutationSettingIsVerifiedAtBirthWithoutAssumingVisibleAdaptation(double amplitude)
    {
        var settings = ExperimentSettings.Create(new(ControllerMode.Neural, amplitude));
        var brain = NeuralGenome.CreateZero();
        var parent = new Organism(11, 500, 350, 0, 200, 0, 0, 30, 0, brain);
        var simulation = new Simulation(settings, new WorldState(1000, 700, [parent], []));
        Assert.Equal(0, simulation.GetStatistics().TotalBirths); simulation.AdvanceTick();
        Assert.Equal(1, simulation.GetStatistics().TotalBirths);
        var child = simulation.State.World.Organisms[1];
        if (amplitude == 0) Assert.Same(brain, child.Brain);
        else Assert.NotSame(brain, child.Brain);
    }
}
