using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralSteeringTests
{
    [Fact]
    public void ZeroBrainBypassesTaskNineRatherThanFallingBack()
    {
        var organism = NeuralFixture.Organism(speed: 0, heading: 0);
        Food[] food = [new(0, 50, 41, 20)];
        var neural = NeuralFixture.Create([organism], food);
        var legacy = NeuralFixture.Create([organism], food, NeuralFixture.Settings(enabled: false, food: 1));
        neural.AdvanceTick(); legacy.AdvanceTick();
        Assert.Equal(0, neural.State.World.Organisms[0].HeadingRadians);
        Assert.Equal(Math.PI / 2, legacy.State.World.Organisms[0].HeadingRadians);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(-0.25)]
    [InlineData(-0.5)]
    public void SignedCommandScalesAllowedRotationWithoutFood(double bias)
    {
        var organism = NeuralFixture.Organism(NeuralFixture.Brain(bias), speed: 0, heading: 2, steering: 0.75);
        var simulation = NeuralFixture.Create([organism], settings: NeuralFixture.Settings(duration: 0.5));
        simulation.AdvanceTick();
        NeuralFixture.Near(2 + Math.Tanh(bias) * 0.375, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Equal((50.0, 40.0), (simulation.State.World.Organisms[0].X, simulation.State.World.Organisms[0].Y));
    }

    [Fact]
    public void ZeroRateAndZeroCommandPreserveExactHeadingBits()
    {
        var heading = Math.BitDecrement(2 * Math.PI);
        foreach (var organism in new[] { NeuralFixture.Organism(NeuralFixture.Brain(8), speed: 0, heading: heading, steering: 0),
            NeuralFixture.Organism(speed: 0, heading: heading) })
        {
            var simulation = NeuralFixture.Create([organism]); simulation.AdvanceTick();
            Assert.Equal(BitConverter.DoubleToInt64Bits(heading), BitConverter.DoubleToInt64Bits(simulation.State.World.Organisms[0].HeadingRadians));
        }
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(-0.5)]
    public void SignedRotationWrapsAtFullTurn(double bias)
    {
        var start = bias > 0 ? 2 * Math.PI - 0.1 : 0.1;
        var expected = start + 2 * Math.Tanh(bias);
        if (expected >= 2 * Math.PI) expected -= 2 * Math.PI;
        if (expected < 0) expected += 2 * Math.PI;
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(bias), speed: 0, heading: start)]);
        simulation.AdvanceTick(); NeuralFixture.Near(expected, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void OverflowingSteeringProductSaturatesAtPi()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5), speed: 0, metabolism: 0,
            steering: double.MaxValue, heading: 0)], settings: NeuralFixture.Settings(duration: 2));
        simulation.AdvanceTick(); NeuralFixture.Near(Math.PI * Math.Tanh(0.5), simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void HandSetBrainMatchesIndependentPythonMovementFixture()
    {
        // Python math.tanh/cos/sin, independently applying bias outputs:
        // h=.25+tanh(.5)*2*.1; v=10*(1+.5*tanh(-.25)); x=50+.1*v*cos(h).
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5, -0.25))],
            settings: NeuralFixture.Settings(duration: 0.1));
        var random = simulation.RandomState; simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        NeuralFixture.Near(0.34242343145200194, organism.HeadingRadians);
        NeuralFixture.Near(50.82659391745961, organism.X); NeuralFixture.Near(40.29465254285932, organism.Y);
        Assert.Equal(10, organism.Speed); Assert.Equal(1, organism.Metabolism); Assert.Equal(99.9, organism.Energy);
        Assert.Equal(random, simulation.RandomState);
    }

    [Fact]
    public void EnergyInputUsesPostDrainButPreFeedingEnergy()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.ChannelBrain(3), speed: 0, metabolism: 10,
            energy: 100, heading: 0, steering: 1)], [new Food(0, 50, 40, 20)], NeuralFixture.Settings(food: 1));
        simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        NeuralFixture.Near(Math.Tanh(Math.Tanh(0.8)), organism.HeadingRadians);
        Assert.Equal(110, organism.Energy);
    }

    [Fact]
    public void SensorsUseStartPositionAndFeedingUsesFinalPosition()
    {
        // At x=5, radius=10 the wall channel is zero; after moving it would be -1.
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.ChannelBrain(5), x: 5, speed: 10,
            sensing: 10, heading: 0)], [new Food(0, 15, 40, 20)], NeuralFixture.Settings(food: 1));
        simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        Assert.Equal(0, organism.HeadingRadians); Assert.Equal(15, organism.X); Assert.Equal(119, organism.Energy);
        Assert.Empty(simulation.State.World.FoodItems);
    }

    [Fact]
    public void LaterOrganismCannotSenseFoodConsumedEarlierInTick()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(speed: 0, heading: 0),
            NeuralFixture.Organism(NeuralFixture.ChannelBrain(2), id: 9, speed: 0, heading: 2, steering: 1)],
            [new Food(0, 50, 40, 20)], NeuralFixture.Settings(organisms: 2, food: 1));
        simulation.AdvanceTick();
        NeuralFixture.Near(2 - Math.Tanh(Math.Tanh(1)), simulation.State.World.Organisms[1].HeadingRadians);
        Assert.Equal(119, simulation.State.World.Organisms[0].Energy); Assert.Equal(99, simulation.State.World.Organisms[1].Energy);
    }

    [Fact]
    public void RegeneratedFoodCannotBeSensedUntilFollowingTick()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.ChannelBrain(2), speed: 0, metabolism: 0,
            sensing: double.MaxValue, heading: 2, steering: 1)], settings: NeuralFixture.Settings(foodCap: 1));
        simulation.AdvanceTick(); var first = simulation.State.World.Organisms[0];
        NeuralFixture.Near(2 - Math.Tanh(Math.Tanh(1)), first.HeadingRadians);
        var inputs = NeuralFixture.Inputs(first, simulation.State.World.FoodItems.ToArray(), simulation.Settings);
        Assert.True(inputs[2] > 0); simulation.AdvanceTick();
        NeuralFixture.Near(first.HeadingRadians + Math.Tanh(Math.Tanh(inputs[2])), simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void ReflectionDeterminesHeadingAfterNeuralSteering()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(x: 99, speed: 3, heading: 0, metabolism: 0)]);
        simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        Assert.Equal(98, organism.X); Assert.Equal(Math.PI, organism.HeadingRadians);
    }

    [Fact]
    public void DeathPrecedesNeuralControllerAndOverflowingMovement()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(8, 8), speed: double.MaxValue,
            energy: 1, metabolism: 1)], [new Food(0, 50, 40, 1000)], NeuralFixture.Settings(food: 1));
        var random = simulation.RandomState; simulation.AdvanceTick();
        Assert.Empty(simulation.State.World.Organisms); Assert.Single(simulation.State.World.FoodItems);
        Assert.Equal(random, simulation.RandomState);
    }
}
