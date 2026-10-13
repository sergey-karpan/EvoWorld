using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitMovementTests
{
    [Fact]
    public void IndividualsMoveAtTheirOwnSpeedAndPreserveTraits()
    {
        var simulation = TraitFixture.Create([new Organism(0, 10, 10, 0, 100, 0, 0), new Organism(9, 10, 20, 0, 100, 4, 2)]);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 10.0, 14.0 }, simulation.State.World.Organisms.Select(o => o.X).ToArray());
        Assert.Equal(new[] { (0.0, 0.0), (4.0, 2.0) }, simulation.State.World.Organisms.Select(o => (o.Speed, o.Metabolism)).ToArray());
    }

    [Fact]
    public void TraitDrivenReflectionAndFeedingUsePostMovementPosition()
    {
        var simulation = TraitFixture.Create([new Organism(0, 99, 40, 0, 100, 3, 0)], [new Food(0, 98, 40, 20)]);
        simulation.AdvanceTick(); var organism = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(98, organism.X); Assert.Equal(Math.PI, organism.HeadingRadians); Assert.Equal(120, organism.Energy);
        Assert.Equal(3, organism.Speed); Assert.Empty(simulation.State.World.FoodItems);
    }

    [Fact]
    public void IndividualSpeedProductOverflowRollsBack()
    {
        SurvivalFixture.AssertRollback(TraitFixture.Create([new Organism(0, 10, 10, 0, 100, double.MaxValue, 0)], settings: TraitFixture.Settings(duration: 2)));
    }
}
