using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class DeathTests
{
    [Fact]
    public void FoodCannotRescueExhaustedOrganismAndRemainsAvailableToSurvivor()
    {
        var simulation = SurvivalFixture.Create([new Organism(2, 1, 1, 0, 1), new Organism(9, 1, 1, 0, 3)],
            [new Food(4, 1, 1, 20)], drain: 1);
        simulation.AdvanceTick();
        var survivor = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(9, survivor.Id);
        Assert.Equal(22, survivor.Energy);
        Assert.Empty(simulation.State.World.FoodItems);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void ExhaustedEnergyIsRemovedAndNeverCommittedNegative(double energy, bool survives)
    {
        var simulation = SurvivalFixture.Create([new Organism(7, 1, 1, 0, energy)], drain: 2);
        simulation.AdvanceTick();
        Assert.Equal(survives ? 1 : 0, simulation.State.World.Organisms.Count);
        if (survives) Assert.Equal(1, simulation.State.World.Organisms[0].Energy);
    }

    [Fact]
    public void DeadOrganismCannotFeedOrAttemptOverflowingMovement()
    {
        var food = new Food(9, 1, 1, 20);
        var simulation = SurvivalFixture.Create([new Organism(7, 1, 1, 0, 1)], [food],
            drain: 1, speed: double.MaxValue, duration: 2);
        simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.Organisms);
        Assert.Same(food, Assert.Single(simulation.State.World.FoodItems));
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.TickCount);
    }

    [Fact]
    public void SurvivorIdsAndSnapshotsRemainStableAfterRemovalAndExtinction()
    {
        var simulation = SurvivalFixture.Create(
            [new Organism(0, 1, 1, 0, 1), new Organism(2, 2, 1, 0, 3), new Organism(9, 3, 1, 0, 2)], drain: 1, speed: 1);
        var initial = simulation.State;
        simulation.AdvanceTick();
        Assert.Equal(new[] { 2, 9 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(new[] { 3.0, 4.0 }, simulation.State.World.Organisms.Select(o => o.X).ToArray());
        simulation.AdvanceTick();
        Assert.Equal(2, Assert.Single(simulation.State.World.Organisms).Id);
        simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.Organisms);
        simulation.AdvanceTick();
        Assert.Equal(4, simulation.TickCount);
        Assert.Equal(3, initial.World.Organisms.Count);
        Assert.Equal(1, initial.World.Organisms[0].Energy);
    }

    [Fact]
    public void InternalSeamAcceptsShrunkPopulationsAndAscendingIdGaps()
    {
        var simulation = SurvivalFixture.Create([new Organism(8, 1, 1, 0, 100)], [new Food(12, 9, 9, 20)],
            initialOrganisms: 10, initialFood: 20);
        simulation.AdvanceTick();
        Assert.Equal(8, Assert.Single(simulation.State.World.Organisms).Id);
        Assert.Equal(12, Assert.Single(simulation.State.World.FoodItems).Id);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 2)]
    [InlineData(2, 1)]
    public void InternalSeamRejectsNegativeDuplicateAndUnsortedIds(int first, int second)
    {
        Assert.Throws<ArgumentException>(() => SurvivalFixture.Create(
            [new Organism(first, 1, 1, 0, 10), new Organism(second, 2, 2, 0, 10)]));
        Assert.Throws<ArgumentException>(() => SurvivalFixture.Create([], [new Food(first, 1, 1, 20), new Food(second, 2, 2, 20)]));
    }

    [Fact]
    public void InternalSeamRejectsPopulationsAboveInitialCounts()
    {
        Assert.Throws<ArgumentException>(() => SurvivalFixture.Create([new Organism(0, 1, 1, 0, 10)], initialOrganisms: 0));
        Assert.Throws<ArgumentException>(() => SurvivalFixture.Create([], [new Food(0, 1, 1, 20)], initialFood: 0));
    }
}
