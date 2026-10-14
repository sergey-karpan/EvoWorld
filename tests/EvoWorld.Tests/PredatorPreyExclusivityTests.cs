using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPreyExclusivityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstStableIdCapturesSharedPreyOnceButLaterPredatorStillSensesIt(bool neural)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(id: 7),
            PredationFixture.Predator(id: 99, sensing: 30), PredationFixture.Prey(id: 43)], neural: neural);
        simulation.EnableDiagnosticsFor(99); simulation.AdvanceTick();
        Assert.Equal(new[] { 7, 99 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(160, simulation.State.World.Organisms[0].Energy); Assert.Equal(100, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(1, simulation.GetStatistics().TotalDeaths);
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Null(simulation.LastDiagnosticSample.PreyConsumedId); Assert.Null(simulation.LastDiagnosticSample.EnergyGainedFromPrey);
    }

    [Fact]
    public void NearestEligibleTieUsesSmallestSparseIdAndOnlyOneAttackPerPredator()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(id: 1),
            PredationFixture.Prey(id: 99, x: 49), PredationFixture.Prey(id: 43, x: 51), PredationFixture.Prey(id: int.MaxValue, x: 57)]);
        simulation.EnableDiagnosticsFor(1); simulation.AdvanceTick();
        Assert.Equal(43, simulation.LastDiagnosticSample!.PreyConsumedId);
        Assert.Equal(new[] { 1, 99, int.MaxValue }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId);
    }

    [Fact]
    public void LaterPredatorCanConsumeNextNearestUncapturedPrey()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(id: 1), PredationFixture.Predator(id: 7),
            PredationFixture.Prey(id: 43, x: 51), PredationFixture.Prey(id: 99, x: 52)]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(99, simulation.LastDiagnosticSample!.PreyConsumedId); Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(new[] { 1, 7 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(160, o.Energy));
    }

    [Fact]
    public void SeparatePredatorsCaptureTheirOwnNearbyPrey()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(id: 1, x: 10), PredationFixture.Predator(id: 7, x: 90),
            PredationFixture.Prey(id: 43, x: 11), PredationFixture.Prey(id: 99, x: 89)], radius: 2);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(2, simulation.State.World.Organisms.Count); Assert.All(simulation.State.World.Organisms, o => Assert.Equal(Species.Predator, o.Species));
    }
}
