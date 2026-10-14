using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorAttackTests
{
    [Theory]
    [InlineData(false, 0.0, true)]
    [InlineData(true, 0.0, true)]
    [InlineData(false, 8.0, true)]
    [InlineData(true, 8.0, true)]
    [InlineData(false, 8.000000000000002, false)]
    [InlineData(true, 8.000000000000002, false)]
    public void ContactIsInclusiveAndIndependentOfSensingAndController(bool neural, double distance, bool attacks)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(x: 0), PredationFixture.Prey(x: distance)], neural: neural);
        simulation.EnableDiagnosticsFor(7); var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Equal(attacks ? 1 : 2, simulation.State.World.Organisms.Count);
        Assert.Equal(attacks ? 160 : 100, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(attacks ? 1 : 0, simulation.GetStatistics().TotalPredationEvents);
        Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(attacks ? (int?)43 : null, simulation.LastDiagnosticSample.PreyConsumedId);
        Assert.Equal(random, simulation.RandomState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroRadiusDisablesEvenCoincidentAttacksButHerbivorePickupStillWorks(bool neural)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey()],
            [new Food(0, 50, 40, 20)], neural: neural, radius: 0);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(2, simulation.State.World.Organisms.Count); Assert.Equal(100, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(95, simulation.State.World.Organisms[1].Energy); Assert.Empty(simulation.State.World.FoodItems);
        Assert.Null(simulation.LastDiagnosticSample!.PreyConsumedId); Assert.Null(simulation.LastDiagnosticSample.EnergyGainedFromPrey);
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(2.0, true)]
    [InlineData(3.0, true)]
    public void PickupSettingAloneDeterminesAttackRange(double radius, bool attacks)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey(x: 52)], radius: radius);
        simulation.AdvanceTick(); Assert.Equal(attacks ? 1 : 0, simulation.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrainingPreyAreFilteredBeforeRankingForEitherIdOrder(bool preyFirst)
    {
        var dead = PredationFixture.Prey(id: preyFirst ? 1 : 43, energy: 1, metabolism: 1);
        var viable = PredationFixture.Prey(id: 99, x: 51);
        var simulation = PredationFixture.Create([dead, PredationFixture.Predator(), viable]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(99, simulation.LastDiagnosticSample!.PreyConsumedId);
        Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(2, simulation.GetStatistics().TotalDeaths);
        Assert.Equal(7, Assert.Single(simulation.State.World.Organisms).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExhaustedPredatorCannotAttackBeforeItsOwnDeath(bool neural)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(energy: 1, metabolism: 1), PredationFixture.Prey()], neural: neural);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(43, Assert.Single(simulation.State.World.Organisms).Id);
        Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.False(simulation.LastDiagnosticSample!.DidAct);
    }

    [Fact]
    public void PredatorsNeverAttackThemselvesOrOtherPredators()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Predator(id: 43)], radius: double.MaxValue);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.State.World.Organisms.Count);
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(100, o.Energy)); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(0.0, double.Epsilon, double.Epsilon)]
    [InlineData(0.0, 1e308, 1e308)]
    [InlineData(0.0, 1e308, double.MaxValue)]
    public void SafeDistanceHandlesFiniteExtremeAndSubnormalWorlds(double x, double preyX, double radius)
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(x: x, y: 0), PredationFixture.Prey(x: preyX, y: 0)],
            radius: radius, width: double.MaxValue, height: double.MaxValue);
        simulation.AdvanceTick(); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents);
    }

    [Fact]
    public void DiagonalSubnormalDistanceIsOutsideOneEpsilonRadius()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(x: 0, y: 0),
            PredationFixture.Prey(x: double.Epsilon, y: double.Epsilon)], radius: double.Epsilon, width: 4 * double.Epsilon, height: 4 * double.Epsilon);
        simulation.AdvanceTick(); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents); Assert.Equal(2, simulation.State.World.Organisms.Count);
    }

    [Fact]
    public void DistanceBeyondDoubleRangeIsComparedWithoutOverflow()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(x: 0, y: 0),
            PredationFixture.Prey(x: 1.3e308, y: 1.3e308)], radius: double.MaxValue, width: double.MaxValue, height: double.MaxValue);
        simulation.AdvanceTick(); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
    }

    [Fact]
    public void PredatorAttacksAtReflectedPosition()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(x: 99, speed: 10), PredationFixture.Prey(x: 91)], radius: 0.5);
        simulation.AdvanceTick(); var predator = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(91, predator.X); Assert.Equal(Math.PI, predator.HeadingRadians); Assert.Equal(160, predator.Energy);
    }

    [Fact]
    public void ImpendingMetabolicDeathCannotBeRescuedForAttackEligibilityByCoincidentFood()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(sensing: 30), PredationFixture.Prey(energy: 1, metabolism: 1)],
            [new Food(0, 50, 40, 100)]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Null(simulation.LastDiagnosticSample.PreyConsumedId); Assert.Equal(0, simulation.GetStatistics().TotalPredationEvents);
        Assert.Single(simulation.State.World.FoodItems); Assert.Equal(100, Assert.Single(simulation.State.World.Organisms).Energy);
    }

    [Fact]
    public void SmallestPositivePostDrainEnergyIsStillViable()
    {
        var simulation = PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey(energy: Math.BitIncrement(1.0), metabolism: 1)]);
        simulation.AdvanceTick(); Assert.Equal(1, simulation.GetStatistics().TotalPredationEvents); Assert.Single(simulation.State.World.Organisms);
    }
}
