using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPredationDeterminismTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentMixedRunsMatchCompleteStateAndDiagnosticsForFiveThousandTicks(bool neural)
    {
        var settings = PredationFixture.Settings(neural: neural, cap: 30, foodCap: 40, food: 12,
            mutation: 0.1, initialNeural: 0.5, duration: 0.1);
        var first = new Simulation(settings); var second = new Simulation(settings);
        first.EnableDiagnosticsFor(3); second.EnableDiagnosticsFor(3);
        for (var tick = 0; tick <= 5000; tick++)
        {
            PredationFixture.Same(first, second);
            if (tick < 5000) { first.AdvanceTick(); second.AdvanceTick(); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsHaveNoEffectEvenWhenAttacksActuallyOccur(bool neural)
    {
        Organism[] initial = [PredationFixture.Predator(energy: 200), PredationFixture.Prey(id: 43), PredationFixture.Prey(id: 99)];
        var enabled = PredationFixture.Create(initial, neural: neural, cap: 15, foodCap: 20, mutation: 0.1);
        var disabled = PredationFixture.Create(initial, neural: neural, cap: 15, foodCap: 20, mutation: 0.1);
        enabled.EnableDiagnosticsFor(7);
        for (var tick = 1; tick <= 600; tick++)
        {
            enabled.AdvanceTick(); disabled.AdvanceTick(); PredatorFixture.SameState(enabled, disabled);
            Assert.Null(disabled.LastDiagnosticSample); Assert.Equal(tick, enabled.LastDiagnosticSample!.TickCount);
        }
        Assert.Equal(2, enabled.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(false, 0.0, 6)]
    [InlineData(false, 0.1, 6)]
    [InlineData(true, 0.0, 6)]
    [InlineData(true, 0.1, 80)]
    public void SuccessfulAttackAddsNoDrawsAndRegenerationPrecedesPredatorBirth(bool neural, double mutation, int birthDraws)
    {
        var parent = PredationFixture.Predator(energy: 100, brain: NeuralFixture.Brain(0.2, -0.3));
        var simulation = PredationFixture.Create([parent, PredationFixture.Prey()], neural: neural,
            cap: 3, foodCap: 1, nextOrganismId: 1000, mutation: mutation);
        var reference = new Pcg32(simulation.RandomState);
        var counted = new Pcg32(simulation.RandomState);
        var x = reference.NextUInt() / 4294967296.0 * 100;
        var y = reference.NextUInt() / 4294967296.0 * 80;
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        var food = Assert.Single(simulation.State.World.FoodItems); Assert.Equal((x, y), (food.X, food.Y));
        Assert.Equal(new[] { 7, 1000 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(110, simulation.State.World.Organisms[0].Energy); Assert.Equal(50, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(1, simulation.GetStatistics().TotalBirths); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(1001, simulation.NextOrganismId); Assert.Equal(1, simulation.NextFoodId);
        for (var i = 0; i < 6; i++) reference.NextUInt();
        var child = simulation.State.World.Organisms[1]; Assert.Equal(Species.Predator, child.Species);
        if (neural && mutation > 0)
            NeuralMutationFixture.AssertBits(NeuralMutationFixture.MutatedWeights(parent.Brain, mutation, -1, 1, reference), child.Brain);
        else Assert.Same(parent.Brain, child.Brain);
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
        for (var i = 0; i < 2 + birthDraws; i++) counted.NextUInt();
        Assert.Equal(counted.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void FailureAfterCaptureRollsBackAllCommittedComponentsAndRetryMatchesCleanRun(bool neural, int failure)
    {
        Organism[] initial = [PredationFixture.Predator(), PredationFixture.Prey(x: 65, speed: 10, heading: Math.PI),
            PredationFixture.Predator(id: 99, x: 90, energy: 10)];
        var simulation = PredationFixture.Create(initial, neural: neural, cap: 4, foodCap: 2, mutation: 0.1);
        var clean = PredationFixture.Create(initial, neural: neural, cap: 4, foodCap: 2, mutation: 0.1);
        simulation.EnableDiagnosticsFor(7); clean.EnableDiagnosticsFor(7);
        simulation.AdvanceTick(); clean.AdvanceTick(); var sample = simulation.LastDiagnosticSample;
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.Null(sample!.PreyConsumedId);
        simulation.FailWorldConstructionForTesting = failure == 0;
        simulation.FailSteeringForOrganismIdForTesting = failure == 1 ? 99 : null;
        simulation.FailBeforeMutationForTesting = failure == 2;
        simulation.FailOffspringTraitConstructionForTesting = failure == 3;
        if (failure == 0) NeuralFixture.Rollback<ArgumentNullException>(simulation);
        else if (failure == 3) NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        else NeuralFixture.Rollback<InvalidOperationException>(simulation);
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.FailWorldConstructionForTesting = simulation.FailBeforeMutationForTesting = simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.FailSteeringForOrganismIdForTesting = null;
        simulation.AdvanceTick(); clean.AdvanceTick(); PredationFixture.Same(clean, simulation);
        Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(43, simulation.LastDiagnosticSample!.PreyConsumedId);
        Assert.Equal(1, simulation.GetStatistics().TotalBirths);
    }

    [Fact]
    public void GenuineEnergyOverflowPreservesPreviouslyPublishedDiagnosticAndWorld()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: 1e308),
            PredationFixture.Prey(x: 70, energy: 1e308, speed: 10, heading: Math.PI)], radius: 12, ratio: 1);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick(); var sample = simulation.LastDiagnosticSample;
        Assert.Null(sample!.PreyConsumedId); NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
    }
}
