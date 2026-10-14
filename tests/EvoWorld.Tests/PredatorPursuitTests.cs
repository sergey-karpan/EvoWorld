using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPursuitTests
{
    [Theory]
    [InlineData(50, 50, 0.5)]
    [InlineData(50, 30, 5.783185307179586)]
    [InlineData(40, 40, 0.5)]
    [InlineData(60, 40, 0.0)]
    public void RuleBasedPursuitUsesExistingAngularConventions(double x, double y, double expectedHeading)
    {
        var predator = PursuitFixture.Organism(speed: 2, steering: 0.5);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: x, y: y);
        var simulation = PursuitFixture.Create([predator, prey], pickup: 0); simulation.EnableDiagnosticsFor(7);
        simulation.AdvanceTick(); var current = simulation.State.World.Organisms[0];
        Assert.Equal(expectedHeading, current.HeadingRadians);
        Assert.Equal(50 + 2 * Math.Cos(expectedHeading), current.X);
        Assert.Equal(40 + 2 * Math.Sin(expectedHeading), current.Y);
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal((prey.X, prey.Y, prey.Energy), (simulation.State.World.Organisms[1].X, simulation.State.World.Organisms[1].Y, simulation.State.World.Organisms[1].Energy));
    }

    [Theory]
    [InlineData(0.0, 2.0, TargetKind.None)]
    [InlineData(30.0, 0.0, TargetKind.Prey)]
    public void ZeroRadiusOrSteeringPreservesHeadingButTargetTelemetryIsIndependent(double radius, double rate, TargetKind expected)
    {
        var predator = PursuitFixture.Organism(sensing: radius, steering: rate, heading: 0.25);
        var simulation = PursuitFixture.Create([predator, PursuitFixture.Organism(Species.Herbivore, id: 43, y: 50)]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(0.25, simulation.State.World.Organisms[0].HeadingRadians); Assert.Equal(expected, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Fact]
    public void PredatorWithOnlyFoodAndOtherPredatorsContinuesStraight()
    {
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(speed: 2), PursuitFixture.Organism(id: 43, y: 50)],
            [new Food(0, 50, 50, 20)]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(0, simulation.State.World.Organisms[0].HeadingRadians); Assert.Equal(52, simulation.State.World.Organisms[0].X);
        Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PreyMovementNeverReplacesItsPreviousSnapshotPosition(bool preyFirst, bool neural)
    {
        var predator = PursuitFixture.Organism(id: preyFirst ? 43 : 7, brain: NeuralFixture.ChannelBrain(0));
        var prey = PursuitFixture.Organism(Species.Herbivore, id: preyFirst ? 7 : 43, x: 60, heading: Math.PI / 2, speed: 10);
        var simulation = PursuitFixture.Create(new[] { predator, prey }.OrderBy(o => o.Id).ToArray(), neural: neural);
        var original = simulation.State; simulation.EnableDiagnosticsFor(predator.Id); simulation.AdvanceTick();
        var current = simulation.State.World.Organisms.Single(o => o.Id == predator.Id);
        Assert.Equal(0, current.HeadingRadians); Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(50, simulation.State.World.Organisms.Single(o => o.Id == prey.Id).Y);
        Assert.Equal(40, original.World.Organisms.Single(o => o.Id == prey.Id).Y);
        if (neural) Assert.Equal(0, simulation.LastDiagnosticSample.NeuralOutputSteering);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreyDyingThisTickRemainsEligibleRegardlessOfIdOrder(bool preyFirst)
    {
        var predator = PursuitFixture.Organism(id: preyFirst ? 43 : 7, metabolism: 1, steering: 0.5);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: preyFirst ? 7 : 43, y: 50, energy: 0.5, metabolism: 1);
        var simulation = PursuitFixture.Create(new[] { predator, prey }.OrderBy(o => o.Id).ToArray());
        simulation.EnableDiagnosticsFor(predator.Id); simulation.AdvanceTick();
        var current = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(0.5, current.HeadingRadians); Assert.Equal(99, current.Energy);
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(1, simulation.GetStatistics().TotalDeaths);
        simulation.AdvanceTick(); Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Fact]
    public void NewbornAndMovedParentAreUnavailableUntilNextSnapshot()
    {
        var parent = PursuitFixture.Organism(Species.Herbivore, id: 7, x: 90, speed: 40, heading: Math.PI, energy: 160);
        var predator = PursuitFixture.Organism(id: 43, sensing: 10);
        var simulation = PursuitFixture.Create([parent, predator], cap: 4, pickup: 0);
        simulation.EnableDiagnosticsFor(43); simulation.AdvanceTick();
        Assert.Contains(simulation.State.World.Organisms, o => o.Id == 44);
        Assert.Equal(50, simulation.State.World.Organisms[0].X);
        Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        simulation.AdvanceTick(); Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoincidentPreyIsSensedWithoutAttackDamageOrEnergyTransfer(bool neural)
    {
        var predator = PursuitFixture.Organism(heading: 0.25);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, energy: 75);
        var simulation = PursuitFixture.Create([predator, prey], neural: neural, pickup: 0);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(2, simulation.State.World.Organisms.Count);
        Assert.Equal(100, simulation.State.World.Organisms[0].Energy); Assert.Equal(75, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(0.25, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
    }

    [Fact]
    public void ExtremeFiniteSteeringRateSaturatesSafelyAtHalfTurn()
    {
        var simulation = PursuitFixture.Create([PursuitFixture.Organism(steering: double.MaxValue),
            PursuitFixture.Organism(Species.Herbivore, id: 43, x: 40)], duration: 2);
        simulation.AdvanceTick(); Assert.Equal(Math.PI, simulation.State.World.Organisms[0].HeadingRadians);
    }
}
