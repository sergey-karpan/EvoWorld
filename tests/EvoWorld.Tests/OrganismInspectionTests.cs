using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class OrganismInspectionTests
{
    [Theory]
    [InlineData(100, 80, 1)]
    [InlineData(400, 200, 1)]
    [InlineData(800, 400, 2)]
    [InlineData(75, 200, 1.5)]
    public void HitTestingUsesPhysicalPixelsScaleLetterboxingAndDpi(double width, double height, double dpi)
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(id: 17, x: 30, y: 20)]);
        Assert.True(ViewportTransform.TryCreate(100, 80, width, height, out var view));
        var x = (view.OffsetX + 30 * view.Scale) / dpi;
        var y = (view.OffsetY + 20 * view.Scale) / dpi;
        Assert.Equal(17, OrganismHitTesting.Find(simulation.State.World, width, height, x, y, dpi, dpi));
        Assert.Equal(17, OrganismHitTesting.Find(simulation.State.World, width, height, x + 7 / dpi, y, dpi, dpi));
        Assert.Null(OrganismHitTesting.Find(simulation.State.World, width, height, 0, 0, dpi, dpi));
    }

    [Fact]
    public void HitsUseDistanceThenSmallestIdAndRejectInvalidCoordinates()
    {
        var world = NeuralFixture.Create([NeuralFixture.Organism(id: 7, x: 46), NeuralFixture.Organism(id: 43, x: 54)]).State.World;
        Assert.Equal(7, OrganismHitTesting.Find(world, 100, 80, 50, 40));
        Assert.Equal(43, OrganismHitTesting.Find(world, 100, 80, 52, 40));
        Assert.Null(OrganismHitTesting.Find(world, 100, 80, 90, 40));
        Assert.Null(OrganismHitTesting.Find(world, 0, 80, 50, 40));
        Assert.Null(OrganismHitTesting.Find(world, 100, 80, double.NaN, 40));
        Assert.Null(OrganismHitTesting.Find(world, 100, 80, 50, 40, 0));
    }

    [Fact]
    public void SelectionSeedsAndResolvesByIdWithoutChangingSnapshotsOrRng()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(id: 7), NeuralFixture.Organism(id: 43)]);
        var inspection = new OrganismInspection();
        var state = simulation.State; var random = simulation.RandomState;
        inspection.Select(simulation, 43); inspection.Select(simulation, 43);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
        Assert.Equal(1, inspection.Count); Assert.Equal(0, inspection[0].TickCount);
        Assert.Equal(43, inspection.LastAlive!.Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => inspection.Select(simulation, -1));
        simulation.AdvanceTick(); inspection.Observe(simulation);
        Assert.NotSame(state.World.Organisms[1], simulation.State.World.Organisms[1]);
        Assert.Equal(43, inspection.LastAlive.Id);
        Assert.Equal(simulation.State.World.Organisms[1].X, inspection.LastAlive.X);
        Assert.Equal(50, state.World.Organisms[1].X);
        inspection.Select(simulation, null);
        Assert.Null(inspection.SelectedId); Assert.Equal(0, inspection.Count);
        Assert.Null(inspection.LastAlive); Assert.Null(inspection.LastActed); Assert.Null(simulation.DiagnosticOrganismId);
    }

    [Fact]
    public void FiveHundredPointRingDiscardsOldestAndIgnoresRepeatedObservation()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(metabolism: 0, speed: 0)]);
        var inspection = new OrganismInspection(); inspection.Select(simulation, 0);
        for (var tick = 1; tick <= 650; tick++)
        {
            simulation.AdvanceTick(); inspection.Observe(simulation); inspection.Observe(simulation);
            Assert.Equal(Math.Min(500, tick + 1), inspection.Count);
        }
        Assert.Equal(151, inspection[0].TickCount); Assert.Equal(650, inspection[499].TickCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => inspection[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => inspection[500]);
        var random = simulation.RandomState; var state = simulation.State;
        for (var i = 0; i < 10; i++) { inspection.Format(simulation); inspection.Observe(simulation); }
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Equal(500, inspection.Count);
    }

    [Fact]
    public void DeathFreezesScalarHistoryAndPreservesClearlyTimestampedCommands()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.3, 0.2), energy: 2.5)]);
        var inspection = new OrganismInspection(); inspection.Select(simulation, 0);
        simulation.AdvanceTick(); inspection.Observe(simulation);
        simulation.AdvanceTick(); inspection.Observe(simulation);
        var alive = inspection.LastAlive; var acted = inspection.LastActed;
        for (var i = 0; i < 4; i++) { simulation.AdvanceTick(); inspection.Observe(simulation); }
        Assert.False(inspection.IsAlive); Assert.Equal(3, inspection.Count);
        Assert.Same(alive, inspection.LastAlive); Assert.Same(acted, inspection.LastActed);
        Assert.False(simulation.LastDiagnosticSample!.DidAct); Assert.Null(simulation.LastDiagnosticSample.EffectiveSpeed);
        var text = inspection.Format(simulation);
        Assert.Contains("Organism #0 — deceased | Tick 6", text);
        Assert.Contains("Historical last alive state at tick 2", text);
        Assert.Contains("Last acted at tick 2", text);
        Assert.Contains("N/A (Organism not found)", text);
        Assert.Contains("Energy 0.5", text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NewbornSelectionSeedsOnlyCurrentPositionOrFirstAppearance(bool preselected)
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(id: 17)]);
        var inspection = new OrganismInspection();
        if (preselected) inspection.Select(simulation, 18);
        simulation.AdvanceTick();
        if (preselected) inspection.Observe(simulation); else inspection.Select(simulation, 18);
        Assert.Equal(1, inspection.Count); Assert.Equal(1, inspection[0].TickCount);
        Assert.Equal(simulation.State.World.Organisms[1].X, inspection[0].X);
        Assert.Contains(preselected ? "Did not act this tick" : "No sample yet", inspection.Format(simulation));
        simulation.AdvanceTick(); inspection.Observe(simulation); Assert.Equal(2, inspection.Count);
    }

    [Fact]
    public void UnavailableReasonsIncludeDisabledRuleBasedAndNoSampleYet()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism()], settings: NeuralFixture.Settings(enabled: false));
        var inspection = new OrganismInspection();
        Assert.Equal("Organism not found", inspection.UnavailableReason(simulation));
        inspection.Select(simulation, 0);
        Assert.Equal("No sample yet", inspection.UnavailableReason(simulation));
        simulation.AdvanceTick(); inspection.Observe(simulation);
        Assert.Equal("Rule-based controller", inspection.UnavailableReason(simulation));
        Assert.Contains("N/A (Rule-based controller)", inspection.Format(simulation));
        simulation.DisableDiagnostics(); Assert.Equal("Diagnostics disabled", inspection.UnavailableReason(simulation));
    }

    [Fact]
    public void PendingChoiceAndFailedResetPreserveInspectorSuccessfulResetClearsIt()
    {
        var fail = false;
        var experiment = new DesktopExperiment(settings => fail ? throw new InvalidOperationException() : new Simulation(settings));
        var simulation = experiment.Simulation;
        experiment.Inspection.Select(simulation, 0); experiment.AdvanceTick();
        var sample = simulation.LastDiagnosticSample; var alive = experiment.Inspection.LastAlive;
        experiment.Select(new(ControllerMode.Neural, 0.1));
        Assert.Equal(2, experiment.Inspection.Count); Assert.Same(sample, simulation.LastDiagnosticSample);
        fail = true;
        Assert.Throws<InvalidOperationException>(experiment.Reset);
        Assert.Same(simulation, experiment.Simulation); Assert.Same(alive, experiment.Inspection.LastAlive);
        Assert.Same(sample, simulation.LastDiagnosticSample); Assert.Equal(0, experiment.Inspection.SelectedId);
        fail = false; experiment.Reset();
        Assert.NotSame(simulation, experiment.Simulation); Assert.Null(experiment.Inspection.SelectedId);
        Assert.Equal(0, experiment.Inspection.Count); Assert.Null(simulation.DiagnosticOrganismId);
        Assert.Null(experiment.Simulation.LastDiagnosticSample);
    }

    [Fact]
    public void FailedTickDoesNotAppendOrReplaceLastActedSample()
    {
        var experiment = new DesktopExperiment();
        experiment.Inspection.Select(experiment.Simulation, 0); experiment.AdvanceTick();
        var alive = experiment.Inspection.LastAlive; var sample = experiment.Inspection.LastActed;
        experiment.Simulation.FailWorldConstructionForTesting = true;
        Assert.Throws<ArgumentNullException>(experiment.AdvanceTick);
        Assert.Equal(2, experiment.Inspection.Count);
        Assert.Same(alive, experiment.Inspection.LastAlive); Assert.Same(sample, experiment.Inspection.LastActed);
        experiment.Simulation.FailWorldConstructionForTesting = false;
        experiment.AdvanceTick(); Assert.Equal(3, experiment.Inspection.Count);
    }

    [Fact]
    public void ChangingSelectedIdClearsAllHistoricalStateAndOldTelemetry()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(id: 7), NeuralFixture.Organism(id: 43, x: 70)]);
        var inspection = new OrganismInspection(); inspection.Select(simulation, 7);
        simulation.AdvanceTick(); inspection.Observe(simulation);
        Assert.NotNull(inspection.LastActed);
        inspection.Select(simulation, 43);
        Assert.Equal(1, inspection.Count); Assert.Null(inspection.LastActed); Assert.Null(simulation.LastDiagnosticSample);
        Assert.Equal(43, inspection.LastAlive!.Id); Assert.Equal(1, inspection[0].TickCount);
        Assert.Contains("No sample yet", inspection.Format(simulation));
    }

    [Fact]
    public void AnisotropicDpiAndWorldBoundaryRejectLetterboxClicks()
    {
        var world = NeuralFixture.Create([NeuralFixture.Organism(x: 50, y: 40)]).State.World;
        Assert.Equal(0, OrganismHitTesting.Find(world, 200, 160, 50, 40 / 1.5 * 2, 2, 1.5));
        Assert.Null(OrganismHitTesting.Find(world, 300, 160, 49, 80));
        Assert.Null(OrganismHitTesting.Find(world, 300, 160, 250, 80));
    }
}
