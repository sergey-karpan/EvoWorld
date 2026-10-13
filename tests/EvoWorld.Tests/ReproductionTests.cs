using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class ReproductionTests
{
    [Fact]
    public void EmptyWorldCannotProduceOrganismsWhileFoodCanRegenerate()
    {
        var simulation = ReproductionFixture.Create([], foodCap: 2);
        simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.Organisms); Assert.Single(simulation.State.World.FoodItems);
        Assert.Equal(0, simulation.NextOrganismId);
    }

    [Theory]
    [InlineData(149, 150, 50, 1, 149)]
    [InlineData(150, 150, 50, 2, 100)]
    [InlineData(160, 150, 50, 2, 110)]
    [InlineData(90, 80, 100, 1, 90)]
    [InlineData(150, 150, 150, 1, 150)]
    public void EligibilityUsesThresholdAndStrictlyPositivePostCostEnergy(double energy, double threshold,
        double cost, int count, double parentEnergy)
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 1, energy)], threshold: threshold, cost: cost);
        var random = new Pcg32(simulation.RandomState); var nextId = simulation.NextOrganismId;
        simulation.AdvanceTick();
        Assert.Equal(count, simulation.State.World.Organisms.Count);
        Assert.Equal(parentEnergy, simulation.State.World.Organisms[0].Energy);
        if (count == 2) { random.NextUInt(); random.NextUInt(); random.NextUInt(); random.NextUInt(); random.NextUInt(); random.NextUInt(); Assert.Equal(50, simulation.State.World.Organisms[1].Energy); }
        Assert.Equal(random.CaptureState(), simulation.RandomState);
        Assert.Equal(nextId + count - 1, simulation.NextOrganismId);
    }

    [Theory]
    [InlineData(50, 50, 160)]
    [InlineData(60, 40, 140)]
    public void EnergyIsTransferredWithOptionalOverhead(double cost, double childEnergy, double total)
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160)], cost: cost, childEnergy: childEnergy);
        simulation.AdvanceTick();
        Assert.Equal(160 - cost, simulation.State.World.Organisms[0].Energy);
        Assert.Equal(childEnergy, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(total, simulation.State.World.Organisms.Sum(o => o.Energy));
    }

    [Fact]
    public void FeedingAndDrainPrecedeBirthAndOriginalSnapshotRemainsImmutable()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 140)], [new Food(0, 50, 40, 20)], drain: 1);
        var previous = simulation.State;
        simulation.AdvanceTick();
        Assert.Equal(109, simulation.State.World.Organisms[0].Energy); // 140 - 1 + 20 - 50.
        Assert.Equal(50, simulation.State.World.Organisms[1].Energy);
        Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(140, previous.World.Organisms[0].Energy);
        Assert.Single(previous.World.Organisms); Assert.Single(previous.World.FoodItems);
    }

    [Fact]
    public void DrainBelowThresholdAndDeathPreventBirths()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 150), new Organism(9, 2, 2, 0, 1)], drain: 1);
        var random = simulation.RandomState;
        simulation.AdvanceTick();
        Assert.Equal(149, Assert.Single(simulation.State.World.Organisms).Energy);
        Assert.Equal(random, simulation.RandomState);
        var extinct = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 1)], [new Food(0, 1, 1, 1000)], drain: 1, foodCap: 2);
        extinct.AdvanceTick();
        Assert.Empty(extinct.State.World.Organisms); Assert.Equal(2, extinct.State.World.FoodItems.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExistingPopulationAtOrAboveCeilingIsPreservedWithoutBirthDraws(int cap)
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 1000), new Organism(9, 2, 2, 0, 1000)], cap: cap);
        var random = simulation.RandomState; var nextId = simulation.NextOrganismId;
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.State.World.Organisms.Count);
        Assert.All(simulation.State.World.Organisms, o => Assert.Equal(1000, o.Energy));
        Assert.Equal(random, simulation.RandomState); Assert.Equal(nextId, simulation.NextOrganismId);
    }

    [Fact]
    public void DeathOpensOneSlotAndLowestEligibleParentWins()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 1, 1, 0, 1), new Organism(2, 50, 40, 0, 160), new Organism(9, 60, 40, 0, 160)], drain: 1, cap: 3);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 2, 9, 10 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(new[] { 109.0, 159.0, 50.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
    }

    [Fact]
    public void EachParentProducesAtMostOneChildPerTick()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 1000)]);
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.State.World.Organisms.Count);
        Assert.Equal(950, simulation.State.World.Organisms[0].Energy);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 0, 1, 2 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
    }

    [Fact]
    public void ChildBeginsMovementAndMetabolismOnlyOnFollowingTick()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 160)], drain: 1, speed: 1, cap: 2);
        simulation.AdvanceTick();
        var child = simulation.State.World.Organisms[1];
        Assert.Equal(50, child.Energy);
        Assert.Equal(55.78071537944788, child.X, 12); // Parent moves to X=51 before offset.
        Assert.Equal(38.53549992806059, child.Y, 12);
        simulation.AdvanceTick();
        var next = simulation.State.World.Organisms[1];
        Assert.Equal(49, next.Energy);
        Assert.True(next.X > child.X && next.Y > child.Y);
        Assert.Equal(child.HeadingRadians, next.HeadingRadians);
    }

    [Fact]
    public void IndependentThreeTickFixtureDemonstratesFeedingBirthAndChildActivity()
    {
        // Python PCG reference: initial draw count=3+2*2=7. Birth draws are
        // 1938801432 and 186621015. Child offset=(-4.768806671505141,1.5028249830928622).
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 131)],
            [new Food(0, 50, 40, 20), new Food(1, 45.23119332849486, 41.502824983092864, 20)], drain: 1, radius: 1e-9, cap: 2);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 100.0, 50.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        var child = simulation.State.World.Organisms[1];
        Assert.Equal(45.23119332849486, child.X, 12); Assert.Equal(41.502824983092864, child.Y, 12);
        Assert.Equal(6.191875525387944, child.HeadingRadians, 14);
        Assert.Equal(1, Assert.Single(simulation.State.World.FoodItems).Id);
        Assert.Equal(new Pcg32State(3844881371314758523UL, 3), simulation.RandomState);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 99.0, 69.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        Assert.Empty(simulation.State.World.FoodItems);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 98.0, 68.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        Assert.Equal(new[] { 0, 1 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(2, simulation.NextOrganismId);
        Assert.Equal(new Pcg32State(3844881371314758523UL, 3), simulation.RandomState);
    }
}
