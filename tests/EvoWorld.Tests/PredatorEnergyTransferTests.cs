using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorEnergyTransferTests
{
    [Theory]
    [InlineData(false, false, 0.0, 95.0)]
    [InlineData(true, false, 0.0, 95.0)]
    [InlineData(false, true, 0.0, 95.0)]
    [InlineData(true, true, 0.0, 95.0)]
    [InlineData(false, false, 0.8, 155.0)]
    [InlineData(true, false, 0.8, 155.0)]
    [InlineData(false, true, 0.8, 155.0)]
    [InlineData(true, true, 0.8, 155.0)]
    [InlineData(false, false, 1.0, 170.0)]
    [InlineData(true, false, 1.0, 170.0)]
    [InlineData(false, true, 1.0, 170.0)]
    [InlineData(true, true, 1.0, 170.0)]
    public void IndependentScalarFixtureUsesSnapshotEnergyDespitePreyDrainAndFeeding(bool preyFirst, bool neural, double ratio, double expected)
    {
        // Independent reference: predator 100 - (10 * .5) = 95;
        // committed prey 75 gives gains 0, 60, 75. Lower-ID prey first
        // drains to 73 and eats 20 (93); neither 73 nor 93 is credited.
        var predator = PredationFixture.Predator(id: preyFirst ? 43 : 7, metabolism: 10);
        var prey = PredationFixture.Prey(id: preyFirst ? 7 : 43, metabolism: 4);
        var simulation = PredationFixture.Create([predator, prey], [new Food(0, 50, 40, 20)],
            neural: neural, ratio: ratio, duration: 0.5);
        var initial = simulation.State; var random = simulation.RandomState;
        simulation.EnableDiagnosticsFor(predator.Id); simulation.AdvanceTick();
        var current = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(expected, current.Energy); Assert.Equal(predator.Id, current.Id);
        Assert.Equal(expected - 95, simulation.LastDiagnosticSample!.EnergyGainedFromPrey);
        Assert.Equal(prey.Id, simulation.LastDiagnosticSample.PreyConsumedId);
        Assert.Equal(preyFirst ? 0 : 1, simulation.State.World.FoodItems.Count);
        Assert.Equal(1, simulation.GetStatistics().TotalDeaths); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(75, initial.World.Organisms.Single(o => o.Id == prey.Id).Energy);
        Assert.Single(initial.World.FoodItems);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.Epsilon)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ExtremeFinitePreyEnergyHasSafeProductAndFiniteSumWhenPossible(double ratio)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: double.Epsilon),
            PredationFixture.Prey(energy: double.MaxValue)], ratio: ratio);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        var gain = ratio * double.MaxValue;
        Assert.Equal(gain, simulation.LastDiagnosticSample!.EnergyGainedFromPrey);
        Assert.Equal(double.Epsilon + gain, Assert.Single(simulation.State.World.Organisms).Energy);
        Assert.True(double.IsFinite(simulation.State.World.Organisms[0].Energy));
    }

    [Fact]
    public void UnderflowedGainStillCapturesAndRecordsNonNullZero()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey(energy: double.Epsilon)], ratio: 0.25);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(0.0, simulation.LastDiagnosticSample!.EnergyGainedFromPrey);
        Assert.Equal(43, simulation.LastDiagnosticSample.PreyConsumedId); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowingAdditionRollsBackAndRepeatedFailureIsDeterministic(bool neural)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: double.MaxValue),
            PredationFixture.Prey(energy: double.MaxValue)], neural: neural, ratio: 1);
        simulation.EnableDiagnosticsFor(7);
        NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Null(simulation.LastDiagnosticSample); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
        NeuralFixture.Rollback<OverflowException>(simulation); // Deterministic repeated failure.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HypotheticalPreyDrainOverflowFailsEntireTickBeforeCapture(bool preyFirst)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(id: preyFirst ? 43 : 7),
            PredationFixture.Prey(id: preyFirst ? 7 : 43, energy: double.MaxValue, metabolism: double.MaxValue)], duration: 2);
        simulation.EnableDiagnosticsFor(preyFirst ? 43 : 7);
        NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.Null(simulation.LastDiagnosticSample);
    }
}
