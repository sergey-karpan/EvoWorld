using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPredationStatisticsTests
{
    [Fact]
    public void ConstructorsStartAtZeroAndReadsDoNotChangeStateOrRng()
    {
        var simulation = new Simulation(PredationFixture.Settings());
        var custom = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey()]);
        foreach (var run in new[] { simulation, custom })
        {
            var world = run.State; var random = run.RandomState;
            Assert.Equal(0, run.GetStatistics(out var snapshot).TotalPredationEvents);
            Assert.Same(world, snapshot); Assert.Same(world, run.State); Assert.Equal(random, run.RandomState);
        }
    }

    [Fact]
    public void SuccessfulCapturesAccumulateIndependentlyOfStarvationAndPersistAfterExtinction()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: 3, metabolism: 1),
            PredationFixture.Prey(id: 43), PredationFixture.Prey(id: 99), PredationFixture.Prey(id: 123, energy: 0.5, metabolism: 1)], ratio: 0);
        simulation.AdvanceTick(); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(2, simulation.GetStatistics().TotalDeaths);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(3, simulation.GetStatistics().TotalDeaths);
        simulation.AdvanceTick(); Assert.Empty(simulation.State.World.Organisms);
        Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(4, simulation.GetStatistics().TotalDeaths);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents);
    }

    [Fact]
    public void LastLongValueIsValidAndNextCaptureOverflowsAtomically()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey(id: 43), PredationFixture.Prey(id: 99)],
            predations: long.MaxValue - 1);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(long.MaxValue, simulation.GetStatistics().TotalPredationEvents);
        var sample = simulation.LastDiagnosticSample;
        NeuralFixture.Rollback<OverflowException>(simulation); Assert.Same(sample, simulation.LastDiagnosticSample);
        Assert.Equal(long.MaxValue, simulation.GetStatistics().TotalPredationEvents);
        NeuralFixture.Rollback<OverflowException>(simulation);
    }

    [Fact]
    public void ExhaustedCounterDoesNotPreventTicksWithoutAttack()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey()], radius: 0, predations: long.MaxValue);
        simulation.AdvanceTick(); Assert.Equal(long.MaxValue, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(1, simulation.TickCount);
    }

    [Fact]
    public void RecordEqualityIncludesPredationsWhileLegacyConstructorDefaultsZero()
    {
        var state = new SimulationState(new WorldState(100, 80, [], []), 0);
        var original = new PopulationStatistics(state, 0, 0);
        Assert.Equal(original, new PopulationStatistics(state, 0, 0, 0));
        Assert.NotEqual(original, new PopulationStatistics(state, 0, 0, 1));
        Assert.Null(typeof(PopulationStatistics).GetProperty(nameof(PopulationStatistics.TotalPredationEvents))!.SetMethod);
    }

    [Fact]
    public void NegativeInjectedCounterIsRejected()
    {
        Assert.Equal("totalPredationEvents", Assert.Throws<ArgumentOutOfRangeException>(() =>
            PredationFixture.Create([], predations: -1)).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoPredatorsNeverCreatePredationEvents(bool neural)
    {
        var simulation = new Simulation(PredationFixture.Settings(neural: neural, herbivores: 5, predators: 0, cap: 20, foodCap: 40, food: 10));
        for (var i = 0; i < 100; i++) { simulation.AdvanceTick(); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); }
    }
}
