using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralInitializationTests
{
    [Theory]
    [InlineData(false, false, false, 10)]
    [InlineData(true, false, false, 14)]
    [InlineData(false, true, false, 14)]
    [InlineData(true, true, false, 18)]
    [InlineData(false, false, true, 158)]
    [InlineData(true, false, true, 162)]
    [InlineData(false, true, true, 162)]
    [InlineData(true, true, true, 166)]
    public void EightModesPreserveLayoutAndVerifyEveryTraitWeightAndDraw(bool oldVariation, bool newVariation, bool neural, int draws)
    {
        var settings = NeuralFixture.Settings(neural, organisms: 2, food: 2,
            oldSpeedVariation: oldVariation ? 10 : 0, oldMetabolismVariation: oldVariation ? 0.5 : 0,
            sensingVariation: newVariation ? 10 : 0, steeringVariation: newVariation ? 0.5 : 0);
        var random = new Pcg32(42); var reference = new Pcg32(42);
        var world = WorldInitializer.Create(settings, random);
        var plain = WorldInitializer.Create(NeuralFixture.Settings(false, organisms: 2, food: 2));
        Assert.Equal(plain.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians)), world.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians)));
        Assert.Equal(plain.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)), world.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
        for (var i = 0; i < 10; i++) reference.NextUInt();
        foreach (var organism in world.Organisms)
        {
            Assert.Equal(oldVariation ? 10 + Signed(reference) * 10 : 10, organism.Speed);
            Assert.Equal(oldVariation ? 1 + Signed(reference) * 0.5 : 1, organism.Metabolism);
        }
        foreach (var organism in world.Organisms)
        {
            Assert.Equal(newVariation ? 30 + Signed(reference) * 10 : 30, organism.SensingRadius);
            Assert.Equal(newVariation ? 2 + Signed(reference) * 0.5 : 2, organism.SteeringRate);
        }
        foreach (var organism in world.Organisms)
            foreach (var weight in organism.Brain.Weights)
                Assert.Equal(neural ? Math.Clamp(Signed(reference) * 0.5, -1, 1) : 0, weight);
        Assert.Equal(reference.CaptureState(), random.CaptureState());
        var countReference = new Pcg32(42); for (var i = 0; i < draws; i++) countReference.NextUInt();
        Assert.Equal(countReference.CaptureState(), random.CaptureState());
        if (neural) Assert.NotSame(world.Organisms[0].Brain, world.Organisms[1].Brain);
        else Assert.Same(world.Organisms[0].Brain, world.Organisms[1].Brain);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentPythonNeuralWeightGoldenChecksPhaseStartAndEnd(bool combined)
    {
        // Separate Python integer PCG-XSH-RR setseq reference, seed=42, stream=1.
        // Plain: 3 placement draws, then 74 weights. Combined: 5 placement,
        // two Task 8, two Task 9 draws, then 74 weights. Weight=output/2^32-.5.
        var settings = NeuralFixture.Settings(food: combined ? 1 : 0,
            oldSpeedVariation: combined ? 10 : 0, oldMetabolismVariation: combined ? 0.5 : 0,
            sensingVariation: combined ? 10 : 0, steeringVariation: combined ? 0.5 : 0);
        var random = new Pcg32(42); var organism = WorldInitializer.Create(settings, random).Organisms[0];
        double[] first = combined
            ? [-0.24339357018470764, 0.24555835966020823, -0.14486683486029506, -0.4197141381446272]
            : [0.452689845347777, 0.4039041670039296, -0.08206339739263058, 0.027621812652796507];
        double[] last = combined
            ? [0.470440236851573, 0.01000259886495769, -0.26776653761044145, 0.0015325190033763647]
            : [-0.40547324949875474, 0.17648446070961654, 0.08901994954794645, 0.29142194730229676];
        Assert.Equal(first, organism.Brain.Weights[..4].ToArray()); Assert.Equal(last, organism.Brain.Weights[^4..].ToArray());
        Assert.Equal(new Pcg32State(combined ? 6339629536316825777UL : 15105860163977123515UL, 3), random.CaptureState());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EnabledZeroNeuralAmplitudeConsumesNoNeuralDraws(bool oldVariation, bool newVariation)
    {
        SimulationSettings Settings(bool enabled) => NeuralFixture.Settings(enabled, organisms: 2, food: 2, amplitude: 0,
            oldSpeedVariation: oldVariation ? 10 : 0, sensingVariation: newVariation ? 10 : 0);
        var enabled = new Simulation(Settings(true)); var disabled = new Simulation(Settings(false));
        NeuralFixture.SameState(enabled, disabled);
        Assert.All(enabled.State.World.Organisms, o => Assert.All(o.Brain.Weights.ToArray(), w => Assert.Equal(0, w)));
        Assert.Same(enabled.State.World.Organisms[0].Brain, enabled.State.World.Organisms[1].Brain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(double.MaxValue)]
    public void DisabledNeuralPhaseIgnoresAmplitudeWithoutDraws(double amplitude)
    {
        var random = new Pcg32(42); var expected = new Pcg32(42);
        var world = WorldInitializer.Create(NeuralFixture.Settings(false, organisms: 2, food: 1, amplitude: amplitude), random);
        for (var i = 0; i < 8; i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(), random.CaptureState());
        Assert.All(world.Organisms, o => Assert.All(o.Brain.Weights.ToArray(), w => Assert.Equal(0, w)));
    }

    [Fact]
    public void ExtremeAmplitudeClampsEveryWeightWithoutSkippingOrRetryingDraws()
    {
        var settings = NeuralFixture.Settings(amplitude: double.MaxValue, weightMin: -8, weightMax: 8);
        var random = new Pcg32(42); var reference = new Pcg32(42);
        var world = WorldInitializer.Create(settings, random); for (var i = 0; i < 3; i++) reference.NextUInt();
        foreach (var weight in world.Organisms[0].Brain.Weights)
        {
            var signed = Signed(reference);
            Assert.Equal(Math.Clamp(signed * double.MaxValue, -8, 8), weight); Assert.True(double.IsFinite(weight));
        }
        Assert.Equal(reference.CaptureState(), random.CaptureState());
    }

    [Theory]
    [InlineData(-8, 0)]
    [InlineData(0, 8)]
    public void ZeroAtRangeEndpointStillConsumesAllNeuralDraws(double min, double max)
    {
        var random = new Pcg32(42); var reference = new Pcg32(42);
        var world = WorldInitializer.Create(NeuralFixture.Settings(weightMin: min, weightMax: max), random);
        for (var i = 0; i < 77; i++) reference.NextUInt();
        Assert.Equal(reference.CaptureState(), random.CaptureState());
        Assert.All(world.Organisms[0].Brain.Weights.ToArray(), w => Assert.InRange(w, min, max));
    }

    [Fact]
    public void EmptyInitialPopulationConsumesNoNeuralDraws()
    {
        var random = new Pcg32(42); var reference = new Pcg32(42);
        var world = WorldInitializer.Create(NeuralFixture.Settings(organisms: 0, food: 1), random);
        reference.NextUInt(); reference.NextUInt(); Assert.Equal(reference.CaptureState(), random.CaptureState());
        Assert.Empty(world.Organisms); Assert.Single(world.FoodItems);
    }

    private static double Signed(Pcg32 random) => 2 * (random.NextUInt() / 4294967296.0) - 1;
}
