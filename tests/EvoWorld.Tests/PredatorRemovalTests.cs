using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorRemovalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SequentialPositionDependsOnPreySlotAndDoesNotMovePreyPrematurely(bool preyFirst, bool startsInRange)
    {
        var prey = PredationFixture.Prey(id: preyFirst ? 1 : 43, x: startsInRange ? 50 : 70,
            speed: 20, heading: startsInRange ? 0 : Math.PI);
        var simulation = PredationFixture.Create([PredationFixture.Predator(), prey], radius: 1);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        var attack = preyFirst ? !startsInRange : startsInRange;
        Assert.Equal(attack ? 1 : 0, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(attack ? (int?)prey.Id : null, simulation.LastDiagnosticSample!.PreyConsumedId);
        if (!attack) Assert.Equal(startsInRange ? 70 : 50, simulation.State.World.Organisms.Single(o => o.Id == prey.Id).X);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedHigherIdNeverMovesFeedsOrReproducesAndIdsAreNotReused(bool neural)
    {
        var prey = PredationFixture.Prey(id: 999, energy: 200, speed: double.MaxValue);
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: 10), prey],
            [new Food(0, 50, 40, 20)], neural: neural, ratio: 0, duration: 2, cap: 4, nextOrganismId: 1200);
        var random = simulation.RandomState; simulation.EnableDiagnosticsFor(999); simulation.AdvanceTick();
        Assert.Equal(7, Assert.Single(simulation.State.World.Organisms).Id); Assert.Single(simulation.State.World.FoodItems);
        Assert.Equal(1200, simulation.NextOrganismId); Assert.Equal(0, simulation.GetStatistics().TotalBirths);
        Assert.Equal(random, simulation.RandomState); Assert.False(simulation.LastDiagnosticSample!.DidAct);
        Assert.Null(simulation.LastDiagnosticSample.PreyConsumedId);
    }

    [Fact]
    public void CapturedLowerIdKeepsItsCompletedFoodConsumptionAndCannotReproduce()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 1, energy: 200, metabolism: 1),
            PredationFixture.Predator(energy: 10)], [new Food(0, 50, 40, 20)], ratio: 0, cap: 4);
        simulation.EnableDiagnosticsFor(1); var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Equal(7, Assert.Single(simulation.State.World.Organisms).Id); Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(0, simulation.GetStatistics().TotalBirths); Assert.Equal(8, simulation.NextOrganismId);
        Assert.Equal(random, simulation.RandomState); Assert.True(simulation.LastDiagnosticSample!.DidAct);
    }

    [Fact]
    public void FinalSurvivorOrderingAndRemovalOfMultipleLowerIdPreyAreStable()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 1), PredationFixture.Prey(id: 7, x: 52),
            PredationFixture.Predator(id: 43), PredationFixture.Predator(id: 99), PredationFixture.Prey(id: 123, x: 90)]);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 43, 99, 123 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(2, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(2, simulation.GetStatistics().TotalDeaths);
        Assert.Equal(75, simulation.State.World.Organisms[2].Energy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotSensingAndCurrentAttackCanChooseDifferentPrey(bool neural)
    {
        var nearSnapshot = PredationFixture.Prey(id: 1, x: 51, speed: 20);
        var nearCurrent = PredationFixture.Prey(id: 7, x: 70, speed: 20, heading: Math.PI);
        var predator = PredationFixture.Predator(id: 43, sensing: 30, brain: NeuralFixture.ChannelBrain(2));
        var simulation = PredationFixture.Create([nearSnapshot, nearCurrent, predator], neural: neural, radius: 1);
        simulation.EnableDiagnosticsFor(43); simulation.AdvanceTick();
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(7, simulation.LastDiagnosticSample.PreyConsumedId);
        Assert.Equal(new[] { 1, 43 }, simulation.State.World.Organisms.Select(o => o.Id));
        if (neural) Assert.Equal(Math.Tanh(Math.Tanh(1 - 2 * (1.0 / 30))), simulation.LastDiagnosticSample.NeuralOutputSteering);
    }

    [Fact]
    public void SameTickNewbornIsNotAnAttackCandidateAndGetsFreshId()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 1, x: 60, energy: 200),
            PredationFixture.Predator(id: 43, x: 50, energy: 10)], cap: 3, radius: 5, nextOrganismId: 1000);
        simulation.AdvanceTick(); Assert.Equal(new[] { 1, 43, 1000 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(1, simulation.GetStatistics().TotalBirths); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(Species.Herbivore, simulation.State.World.Organisms[2].Species); Assert.Equal(1001, simulation.NextOrganismId);
    }

    [Fact]
    public void NewbornAtPredatorPositionCanOnlyBeCapturedOnFollowingTick()
    {
        // Independent position reference: two initial organisms use six placement
        // draws, then this single birth uses its first draw for the offset angle.
        var reference = new EvoWorld.Core.Random.Pcg32(42);
        for (var i = 0; i < 6; i++) reference.NextUInt();
        var angle = reference.NextUInt() / 4294967296.0 * (2 * Math.PI);
        var x = 60 + 5 * Math.Cos(angle); var y = 40 + 5 * Math.Sin(angle);
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 1, x: 60, energy: 200),
            PredationFixture.Predator(id: 43, x: x, y: y)], radius: 1, cap: 3);
        simulation.EnableDiagnosticsFor(43); simulation.AdvanceTick();
        var child = simulation.State.World.Organisms.Single(o => o.Id == 44);
        Assert.Equal((x, y), (child.X, child.Y)); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
        Assert.Null(simulation.LastDiagnosticSample!.PreyConsumedId);
        simulation.AdvanceTick(); Assert.Equal(44, simulation.LastDiagnosticSample!.PreyConsumedId);
        Assert.Equal(40, simulation.LastDiagnosticSample.EnergyGainedFromPrey); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents);
        // Capture frees a slot: the surviving parent at energy 150 births again.
        Assert.Equal(new[] { 1, 43, 45 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(2, simulation.GetStatistics().TotalBirths);
        Assert.Equal(46, simulation.NextOrganismId);
    }
}
