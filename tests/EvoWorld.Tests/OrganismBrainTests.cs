using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class OrganismBrainTests
{
    [Fact]
    public void EveryLegacyConstructorUsesSharedImmutableZeroBrain()
    {
        Organism[] organisms = [new(0, 1, 1, 0, 100), new(1, 1, 1, 0, 100, 2, 3), new(2, 1, 1, 0, 100, 2, 3, 4, 5)];
        Assert.All(organisms, o => { Assert.NotNull(o.Brain); Assert.Same(organisms[0].Brain, o.Brain); Assert.All(o.Brain.Weights.ToArray(), w => Assert.Equal(0, w)); });
        Assert.Null(typeof(Organism).GetProperty(nameof(Organism.Brain))!.SetMethod);
    }

    [Fact]
    public void ExplicitBrainIsRetainedAndNullBrainRejected()
    {
        var brain = NeuralFixture.Brain(8, -8);
        Assert.Same(brain, NeuralFixture.Organism(brain).Brain);
        Assert.Equal("brain", Assert.Throws<ArgumentNullException>(() => new Organism(0, 1, 1, 0, 100, 1, 1, 1, 1, null!)).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementDrainFeedingAndParentReplacementPreserveExactBrain(bool enabled)
    {
        var brain = NeuralFixture.Brain(0.5, -0.25); var weights = brain.Weights.ToArray();
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(brain, energy: 160, speed: 0)],
            [new Food(0, 50, 40, 20)], NeuralFixture.Settings(enabled, food: 1, cap: 2));
        simulation.AdvanceTick();
        Assert.All(simulation.State.World.Organisms, o => Assert.Same(brain, o.Brain));
        Assert.Equal(129, simulation.State.World.Organisms[0].Energy); Assert.Empty(simulation.State.World.FoodItems);
        Assert.Equal(weights, brain.Weights.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachChildInheritsOwnParentAndCombinedEventsConsumeFourteenDraws(bool enabled)
    {
        var a = NeuralFixture.Brain(0.5); var b = NeuralFixture.Brain(-0.5);
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(a, speed: 0, energy: 200, id: 11),
            NeuralFixture.Organism(b, speed: 0, energy: 200, id: 99)], settings: NeuralFixture.Settings(enabled, organisms: 2, cap: 4, foodCap: 1));
        var random = new Pcg32(simulation.RandomState);
        for (var i = 0; i < 14; i++) random.NextUInt();
        simulation.AdvanceTick();
        var organisms = simulation.State.World.Organisms;
        Assert.Equal(new[] { 11, 99, 100, 101 }, organisms.Select(o => o.Id));
        Assert.Same(a, organisms[0].Brain); Assert.Same(b, organisms[1].Brain);
        Assert.Same(a, organisms[2].Brain); Assert.Same(b, organisms[3].Brain);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void NewbornFirstRunsBrainOnFollowingTickAndOneBirthUsesSixDraws()
    {
        var brain = NeuralFixture.Brain(0.5);
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(brain, speed: 0, energy: 160)],
            settings: NeuralFixture.Settings(cap: 2, speedMutation: 0, metabolismMutation: 0, sensingMutation: 0, steeringMutation: 0));
        var random = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) random.NextUInt();
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.Equal(50, child.Energy); Assert.Same(brain, child.Brain); Assert.Equal(random.CaptureState(), simulation.RandomState);
        simulation.AdvanceTick(); var grown = simulation.State.World.Organisms[1];
        Assert.Equal(49, grown.Energy); NeuralFixture.Near(child.HeadingRadians + 2 * Math.Tanh(0.5), grown.HeadingRadians);
        Assert.Equal((child.X, child.Y), (grown.X, grown.Y)); Assert.Same(brain, grown.Brain);
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }
}
