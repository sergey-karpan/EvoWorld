using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPredationDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttackFieldsCaptureOutcomeWhileKeepingExactExecutingCommands(bool neural)
    {
        var predator = PredationFixture.Predator(sensing: 30, steering: 2, brain: NeuralFixture.Brain(0.2, -0.3));
        var simulation = PredationFixture.Create([predator, PredationFixture.Prey()], neural: neural);
        var noAttack = PredationFixture.Create([predator, PredationFixture.Prey()], neural: neural, radius: 0);
        simulation.EnableDiagnosticsFor(7); noAttack.EnableDiagnosticsFor(7);
        simulation.AdvanceTick(); noAttack.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!; var old = noAttack.LastDiagnosticSample!;
        Assert.Equal(43, sample.PreyConsumedId); Assert.Equal(60, sample.EnergyGainedFromPrey);
        Assert.Equal(1, sample.TickCount); Assert.True(sample.DidAct); Assert.Equal(TargetKind.Prey, sample.TargetKind);
        Assert.Equal(old.NeuralOutputSteering, sample.NeuralOutputSteering); Assert.Equal(old.NeuralOutputSpeed, sample.NeuralOutputSpeed);
        Assert.Equal(old.AppliedRotationRadians, sample.AppliedRotationRadians); Assert.Equal(old.EffectiveSpeed, sample.EffectiveSpeed);
        if (neural)
        {
            Assert.Equal(Math.Tanh(0.2), sample.NeuralOutputSteering); Assert.Equal(Math.Tanh(-0.3), sample.NeuralOutputSpeed);
        }
        else { Assert.Null(sample.NeuralOutputSteering); Assert.Null(sample.EffectiveSpeed); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TrackedPreyRetainsItsOwnActionOnlyWhenItsSlotWasCompleted(bool preyFirst, bool neural)
    {
        var preyId = preyFirst ? 1 : 43;
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: preyId, sensing: 30, steering: 2,
            brain: NeuralFixture.Brain(0.1, 0.2)), PredationFixture.Predator()], [new Food(0, 50, 40, 20)], neural: neural);
        simulation.EnableDiagnosticsFor(preyId); var initial = simulation.State; simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(preyFirst, sample.DidAct); Assert.Equal(preyFirst ? TargetKind.Food : TargetKind.None, sample.TargetKind);
        Assert.Null(sample.PreyConsumedId); Assert.Null(sample.EnergyGainedFromPrey);
        if (preyFirst && neural) Assert.Equal(Math.Tanh(0.1), sample.NeuralOutputSteering);
        else Assert.Null(sample.NeuralOutputSteering);
        Assert.DoesNotContain(simulation.State.World.Organisms, o => o.Id == preyId);
        Assert.Contains(initial.World.Organisms, o => o.Id == preyId);
        simulation.AdvanceTick(); Assert.False(simulation.LastDiagnosticSample!.DidAct);
        Assert.Null(simulation.LastDiagnosticSample.EnergyGainedFromPrey);
        Assert.Equal(1, sample.TickCount); Assert.Equal(preyFirst, sample.DidAct);
    }

    [Fact]
    public void LegacyConstructionDefaultsNullAndInactiveSamplesIgnoreOutcomeArguments()
    {
        var legacy = new OrganismDiagnosticSample(7, 1, true, false);
        Assert.Null(legacy.PreyConsumedId); Assert.Null(legacy.EnergyGainedFromPrey);
        var inactive = new OrganismDiagnosticSample(7, 1, false, true, targetKind: TargetKind.Prey, preyConsumedId: 43, energyGainedFromPrey: 60);
        Assert.Null(inactive.PreyConsumedId); Assert.Null(inactive.EnergyGainedFromPrey); Assert.Equal(TargetKind.None, inactive.TargetKind);
        var rule = new OrganismDiagnosticSample(7, 1, true, false, preyConsumedId: 43, energyGainedFromPrey: 0);
        Assert.Equal(43, rule.PreyConsumedId); Assert.Equal(0, rule.EnergyGainedFromPrey); Assert.Null(rule.NeuralOutputSteering);
        Assert.All(typeof(OrganismDiagnosticSample).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulAttackFollowedByNoAttackDoesNotReuseOutcome(bool neural)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey()], neural: neural);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick(); var original = simulation.LastDiagnosticSample!;
        simulation.AdvanceTick(); Assert.Null(simulation.LastDiagnosticSample!.PreyConsumedId); Assert.Null(simulation.LastDiagnosticSample.EnergyGainedFromPrey);
        Assert.Equal(43, original.PreyConsumedId); Assert.Equal(60, original.EnergyGainedFromPrey);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InspectionFreezesHistoricalTrailAndResolvesConsumedPreyAsDeceased(bool preyFirst, bool neural)
    {
        var id = preyFirst ? 1 : 43;
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: id, brain: NeuralFixture.Brain(0.1)),
            PredationFixture.Predator()], neural: neural);
        var inspection = new OrganismInspection(); inspection.Select(simulation, id);
        simulation.AdvanceTick(); inspection.Observe(simulation);
        Assert.False(inspection.IsAlive); Assert.Equal(1, inspection.Count); Assert.Equal(0, inspection.LastAlive!.TickCount);
        Assert.Equal(preyFirst, inspection.LastActed is not null); Assert.Contains("deceased", inspection.Format(simulation));
        simulation.AdvanceTick(); inspection.Observe(simulation); Assert.Equal(1, inspection.Count);
        Assert.False(inspection.IsAlive); Assert.Equal(preyFirst, inspection.LastActed is not null);
    }
}
