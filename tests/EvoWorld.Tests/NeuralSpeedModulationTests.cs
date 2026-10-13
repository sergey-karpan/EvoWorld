using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralSpeedModulationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(-0.5)]
    [InlineData(8)]
    [InlineData(-8)]
    public void SpeedCommandChangesOnlyMovementNotInheritedSpeedOrDrain(double bias)
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(speedBias: bias), heading: 0)]);
        simulation.AdvanceTick(); var organism = simulation.State.World.Organisms[0];
        NeuralFixture.Near(50 + 10 * (1 + 0.5 * Math.Tanh(bias)), organism.X);
        Assert.Equal(40, organism.Y); Assert.Equal(10, organism.Speed); Assert.Equal(1, organism.Metabolism); Assert.Equal(99, organism.Energy);
    }

    [Fact]
    public void ZeroModulationIgnoresNonzeroCommand()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(speedBias: 8), heading: 0)],
            settings: NeuralFixture.Settings(modulation: 0));
        simulation.AdvanceTick(); Assert.Equal(60, simulation.State.World.Organisms[0].X);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-8)]
    public void ZeroSpeedRemainsStationaryWithPositiveMultiplier(double bias)
    {
        var organism = NeuralFixture.Organism(NeuralFixture.Brain(speedBias: bias), speed: 0);
        var decision = NeuralControl.Evaluate(organism, 99, [], [], NeuralFixture.Settings(modulation: Math.BitDecrement(1.0)));
        Assert.Equal(0, decision.EffectiveSpeed);
        var simulation = NeuralFixture.Create([organism]); simulation.AdvanceTick();
        Assert.Equal((50.0, 40.0), (simulation.State.World.Organisms[0].X, simulation.State.World.Organisms[0].Y));
    }

    [Fact]
    public void SaturatedMinusOneStillHasPositiveMultiplierAtLargestAllowedAmplitude()
    {
        var weights = new double[74];
        for (var i = 48; i < 56; i++) weights[i] = 8;
        for (var i = 64; i < 72; i++) weights[i] = -8; weights[73] = -8;
        var organism = NeuralFixture.Organism(new NeuralGenome(weights), speed: 1, steering: 0);
        var outputs = new double[2]; organism.Brain.Forward(new double[6], outputs);
        Assert.Equal(-1, outputs[1]);
        var decision = NeuralControl.Evaluate(organism, 100, [], [], NeuralFixture.Settings(modulation: Math.BitDecrement(1.0)));
        Assert.Equal(1 - Math.BitDecrement(1.0), decision.EffectiveSpeed);
        Assert.True(decision.EffectiveSpeed > 0); Assert.True(double.IsFinite(decision.EffectiveSpeed));
    }

    [Fact]
    public void InheritedSpeedInputIsNotPreviouslyModulatedSpeed()
    {
        var brain = NeuralFixture.ChannelBrain(4, output: 1);
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(brain, heading: 0, speed: 50)],
            settings: NeuralFixture.Settings(duration: 0.1));
        var delta = 50 * (1 + 0.5 * Math.Tanh(Math.Tanh(-0.5))) * 0.1;
        simulation.AdvanceTick(); NeuralFixture.Near(50 + delta, simulation.State.World.Organisms[0].X);
        simulation.AdvanceTick(); NeuralFixture.Near(50 + 2 * delta, simulation.State.World.Organisms[0].X);
        Assert.Equal(50, simulation.State.World.Organisms[0].Speed);
    }

    [Fact]
    public void LargeButFiniteEffectiveSpeedCanBeEvaluatedWithoutMovement()
    {
        var organism = NeuralFixture.Organism(NeuralFixture.Brain(speedBias: -8), speed: double.MaxValue);
        var decision = NeuralControl.Evaluate(organism, 99, [], [], NeuralFixture.Settings());
        Assert.True(double.IsFinite(decision.EffectiveSpeed)); Assert.True(decision.EffectiveSpeed > 0);
        Assert.True(decision.EffectiveSpeed < double.MaxValue);
    }
}
