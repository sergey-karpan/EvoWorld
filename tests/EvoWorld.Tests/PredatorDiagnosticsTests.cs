using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorDiagnosticsTests
{
    [Theory]
    [InlineData(Species.Herbivore, false, 30.0, TargetKind.Food)]
    [InlineData(Species.Predator, false, 30.0, TargetKind.Prey)]
    [InlineData(Species.Herbivore, true, 30.0, TargetKind.Food)]
    [InlineData(Species.Predator, true, 30.0, TargetKind.Prey)]
    [InlineData(Species.Herbivore, false, 0.0, TargetKind.None)]
    [InlineData(Species.Predator, false, 0.0, TargetKind.None)]
    [InlineData(Species.Herbivore, true, 0.0, TargetKind.None)]
    [InlineData(Species.Predator, true, 0.0, TargetKind.None)]
    public void ExecutingTargetKindDoesNotDependOnSteeringRateOrControllerMode(Species species, bool neural, double radius, TargetKind expected)
    {
        var actor = PursuitFixture.Organism(species, sensing: radius, steering: 0);
        var other = PursuitFixture.Organism(species == Species.Herbivore ? Species.Predator : Species.Herbivore, id: 43, y: 41);
        var simulation = PursuitFixture.Create([actor, other], [new Food(0, 51, 40, 20)], neural: neural, pickup: 0);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick(); var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(expected, sample.TargetKind); Assert.True(sample.DidAct); Assert.Equal(1, sample.TickCount);
        Assert.Equal(neural, sample.IsNeuralMode); Assert.Equal(!neural, sample.IsRuleBasedMode);
        if (!neural)
        {
            Assert.Null(sample.NeuralOutputSteering); Assert.Null(sample.NeuralOutputSpeed);
            Assert.Null(sample.AppliedRotationRadians); Assert.Null(sample.EffectiveSpeed);
        }
    }

    [Fact]
    public void ConstructorCompatibilityAndReadOnlyTargetKindPreserveDidNotActNulls()
    {
        var old = new OrganismDiagnosticSample(7, 1, true, true, 0.1, 0.2, 0.3, 10);
        Assert.Equal(TargetKind.None, old.TargetKind); Assert.Equal(0.1, old.NeuralOutputSteering);
        var inactive = new OrganismDiagnosticSample(7, 1, false, true, 0.1, 0.2, 0.3, 10, TargetKind.Prey);
        Assert.Equal(TargetKind.None, inactive.TargetKind); Assert.Null(inactive.NeuralOutputSteering); Assert.Null(inactive.EffectiveSpeed);
        var rule = new OrganismDiagnosticSample(7, 1, true, false, targetKind: TargetKind.Food);
        Assert.Equal(TargetKind.Food, rule.TargetKind); Assert.Null(rule.AppliedRotationRadians);
        Assert.Null(typeof(OrganismDiagnosticSample).GetProperty(nameof(OrganismDiagnosticSample.TargetKind))!.SetMethod);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsentDyingAndNewbornObserversHaveNoTarget(bool neural)
    {
        var dying = PursuitFixture.Create([PursuitFixture.Organism(energy: 0.5, metabolism: 1),
            PursuitFixture.Organism(Species.Herbivore, id: 43)], neural: neural);
        dying.EnableDiagnosticsFor(7); dying.AdvanceTick();
        Assert.False(dying.LastDiagnosticSample!.DidAct); Assert.Equal(TargetKind.None, dying.LastDiagnosticSample.TargetKind);
        dying.EnableDiagnosticsFor(999); dying.AdvanceTick();
        Assert.False(dying.LastDiagnosticSample!.DidAct); Assert.Equal(TargetKind.None, dying.LastDiagnosticSample.TargetKind);
        var newborn = PursuitFixture.Create([PursuitFixture.Organism(),
            PursuitFixture.Organism(Species.Herbivore, id: 43, x: 60, energy: 160)], [new Food(0, 51, 40, 20)],
            neural: neural, pickup: 0, cap: 4);
        newborn.EnableDiagnosticsFor(44); newborn.AdvanceTick();
        Assert.Contains(newborn.State.World.Organisms, o => o.Id == 44);
        Assert.False(newborn.LastDiagnosticSample!.DidAct); Assert.Equal(TargetKind.None, newborn.LastDiagnosticSample.TargetKind);
        newborn.AdvanceTick(); Assert.True(newborn.LastDiagnosticSample!.DidAct); Assert.Equal(TargetKind.Food, newborn.LastDiagnosticSample.TargetKind);
    }

    [Fact]
    public void SensedFoodSurvivesInHistoricalSampleAfterFoodIsConsumedAndNextTickHasNoTarget()
    {
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(Species.Herbivore)], [new Food(0, 50, 40, 20)], pickup: 0);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick(); var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(TargetKind.Food, sample.TargetKind); Assert.Empty(simulation.State.World.FoodItems);
        simulation.AdvanceTick(); Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(1, sample.TickCount); Assert.Equal(TargetKind.Food, sample.TargetKind);
    }

    [Fact]
    public void TargetKindIsNotAFeedingOutcomeOrPostMovementInference()
    {
        var blind = PursuitFixture.Create([PursuitFixture.Organism(Species.Herbivore, sensing: 0)], [new Food(0, 50, 40, 20)], pickup: 0);
        blind.EnableDiagnosticsFor(7); blind.AdvanceTick();
        Assert.Equal(TargetKind.None, blind.LastDiagnosticSample!.TargetKind); Assert.Equal(120, blind.State.World.Organisms[0].Energy);
        var moving = PursuitFixture.Create([PursuitFixture.Organism(Species.Herbivore, speed: 10)], [new Food(0, 55, 40, 20)], pickup: 0);
        moving.EnableDiagnosticsFor(7); moving.AdvanceTick();
        Assert.Equal(60, moving.State.World.Organisms[0].X); Assert.Single(moving.State.World.FoodItems);
        Assert.Equal(TargetKind.Food, moving.LastDiagnosticSample!.TargetKind);
    }

    [Fact]
    public void ObserverChangesClearExposureAndTargetKindsNeverLeakBetweenIds()
    {
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(Species.Herbivore), PursuitFixture.Organism(id: 43)],
            [new Food(0, 50, 40, 20)], pickup: 0);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick(); var foodSample = simulation.LastDiagnosticSample!;
        Assert.Equal(TargetKind.Food, foodSample.TargetKind); simulation.EnableDiagnosticsFor(7); Assert.Same(foodSample, simulation.LastDiagnosticSample);
        var random = simulation.RandomState; var state = simulation.State;
        simulation.EnableDiagnosticsFor(43); Assert.Null(simulation.LastDiagnosticSample);
        Assert.Equal(random, simulation.RandomState); Assert.Same(state, simulation.State);
        simulation.AdvanceTick(); Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        simulation.EnableDiagnosticsFor(999); simulation.AdvanceTick(); Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        simulation.DisableDiagnostics(); Assert.Null(simulation.LastDiagnosticSample);
        Assert.Equal(TargetKind.Food, foodSample.TargetKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegeneratedFoodIsNotASensingTargetUntilFollowingTick(bool neural)
    {
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(Species.Herbivore, sensing: double.MaxValue)],
            neural: neural, pickup: 0, foodCap: 1);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Single(simulation.State.World.FoodItems); Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        simulation.AdvanceTick(); Assert.Equal(TargetKind.Food, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void FailureAfterTentativePreyCapturePreservesSampleAndCompleteTransactionThenRetry(int failure, bool neural)
    {
        var settings = PredatorFixture.Settings(neural: neural, food: 0, duration: 0.2);
        Organism[] parents = [PredatorFixture.Organism(Species.Herbivore, id: 7, energy: 500),
            PredatorFixture.Organism(Species.Predator, id: 43, energy: 500)];
        var simulation = PredatorFixture.Create(settings, parents); var clean = PredatorFixture.Create(settings, parents);
        simulation.EnableDiagnosticsFor(43); clean.EnableDiagnosticsFor(43);
        simulation.AdvanceTick(); clean.AdvanceTick(); var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(TargetKind.Prey, sample.TargetKind);
        simulation.FailSteeringForOrganismIdForTesting = failure == 0 ? 43 : null;
        simulation.FailWorldConstructionForTesting = failure == 1;
        simulation.FailOffspringTraitConstructionForTesting = failure == 2;
        if (failure == 0) NeuralFixture.Rollback<InvalidOperationException>(simulation);
        else if (failure == 1) NeuralFixture.Rollback<ArgumentNullException>(simulation);
        else NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.FailSteeringForOrganismIdForTesting = null;
        simulation.FailWorldConstructionForTesting = simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        PredatorFixture.SameState(clean, simulation); PredatorFixture.SameSample(clean.LastDiagnosticSample, simulation.LastDiagnosticSample);
    }

    [Fact]
    public void UntrackedSelectionEncodingAndForwardPassAllocateNoDiagnosticObjectsOrConsumeRng()
    {
        var predator = PursuitFixture.Organism(); var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, y: 50);
        Organism[] previous = [predator, prey]; Food[] food = []; bool[] consumed = [];
        var settings = NeuralFixture.Settings(); OrganismDiagnosticSample? sample = null;
        for (var i = 0; i < 100; i++)
            NeuralControl.Evaluate(predator, 100, TargetSensing.Resolve(predator, food, consumed, previous), settings, -1, 1, ref sample);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            NeuralControl.Evaluate(predator, 100, TargetSensing.Resolve(predator, food, consumed, previous), settings, -1, 1, ref sample);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread()); Assert.Null(sample);
        var simulation = PursuitFixture.Create(previous, neural: true); var random = simulation.RandomState;
        simulation.AdvanceTick(); Assert.Equal(random, simulation.RandomState); Assert.Null(simulation.LastDiagnosticSample);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenuineMovementOverflowAfterPreyCapturePublishesNothing(bool neural)
    {
        var predator = PursuitFixture.Organism(x: 8e307, y: 8e307, speed: 1.3e308, sensing: double.MaxValue);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 9e307, y: 8e307);
        var simulation = PursuitFixture.Create([predator, prey], neural: neural, duration: 1.5,
            width: double.MaxValue, height: double.MaxValue);
        Assert.Equal(TargetKind.Prey, TargetSensing.Resolve(predator, [], [], simulation.State.World.Organisms).Kind);
        simulation.EnableDiagnosticsFor(7);
        // Effective speed is finite, so capture completes; speed*dt then overflows.
        // This is a genuine existing numerical failure, not an injected seam.
        NeuralFixture.Rollback<OverflowException>(simulation); Assert.Null(simulation.LastDiagnosticSample);
    }
}
