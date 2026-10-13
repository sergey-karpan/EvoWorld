using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class OrganismIdContinuityTests
{
    [Fact]
    public void FreshAllocationStartsAtInitialPopulation()
    {
        var simulation = new Simulation(SimulationSettings.CreateDefault());
        Assert.Equal(20, simulation.NextOrganismId);
    }

    [Fact]
    public void HighIdsAndRemovedAllocationHistoryRemainMonotonic()
    {
        var simulation = ReproductionFixture.Create([new Organism(1000, 50, 40, 0, 1000)], nextOrganismId: 5005);
        for (var tick = 1; tick <= 3; tick++)
        {
            simulation.AdvanceTick();
            Assert.Equal(5004 + tick, simulation.State.World.Organisms.Last().Id);
            Assert.Equal(5005 + tick, simulation.NextOrganismId);
        }
        var inferred = ReproductionFixture.Create([new Organism(1000, 50, 40, 0, 160)]);
        Assert.Equal(1001, inferred.NextOrganismId);
    }

    [Fact]
    public void DeathNeverReusesPreviouslyAllocatedIds()
    {
        var simulation = ReproductionFixture.Create([new Organism(2, 1, 1, 0, 1), new Organism(9, 50, 40, 0, 160)], drain: 1);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 9, 10 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(11, simulation.NextOrganismId);
    }

    [Fact]
    public void LastIntIdIsAllocatedAndNextBirthFailsWithoutEffects()
    {
        var simulation = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 1000)], nextOrganismId: int.MaxValue);
        simulation.AdvanceTick();
        Assert.Equal(int.MaxValue, simulation.State.World.Organisms.Last().Id);
        Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId);
        SurvivalFixture.AssertRollback(simulation);
    }

    [Fact]
    public void ExhaustionDoesNotAffectSkippedBirths()
    {
        var capped = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 1000)], cap: 1, nextOrganismId: int.MaxValue + 1L);
        var blocked = ReproductionFixture.Create([new Organism(0, 50, 40, 0, 100)], nextOrganismId: int.MaxValue + 1L);
        foreach (var simulation in new[] { capped, blocked })
        {
            var random = simulation.RandomState; simulation.AdvanceTick();
            Assert.Equal(random, simulation.RandomState); Assert.Equal(int.MaxValue + 1L, simulation.NextOrganismId);
        }
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(8L)]
    [InlineData(2147483649L)]
    public void ExplicitHistoryRejectsInvalidValues(long nextId)
    {
        Assert.Equal("nextOrganismId", Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReproductionFixture.Create([new Organism(9, 50, 40, 0, 160)], nextOrganismId: nextId)).ParamName);
    }

    [Fact]
    public void CustomWorldCanRepresentBirthsBeyondInitialCount()
    {
        var simulation = ReproductionFixture.Create([new Organism(2, 1, 1, 0, 100), new Organism(9, 2, 2, 0, 100)], initialOrganisms: 0);
        Assert.Equal(10, simulation.NextOrganismId);
        simulation.AdvanceTick(); Assert.Equal(2, simulation.State.World.Organisms.Count);
    }
}
