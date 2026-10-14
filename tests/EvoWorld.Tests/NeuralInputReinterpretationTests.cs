using EvoWorld.Core;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public sealed class NeuralInputReinterpretationTests
{
    [Fact]
    public void IndependentSixChannelFixtureMatchesActualExecutingTickCommands()
    {
        var brain = new NeuralGenome(PredatorInputReference.Weights());
        var predator = PursuitFixture.Organism(x: 20, y: 20, speed: 10, metabolism: 2,
            energy: 60, sensing: 10, brain: brain);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 23, y: 24);
        Food[] food = [new(0, 19, 20, 20)];
        var simulation = PursuitFixture.Create([predator, prey], food, neural: true, duration: 0.5);
        var inputs = PursuitFixture.Inputs(predator, simulation.State.World.Organisms, simulation.Settings, food, energy: 59);
        for (var i = 0; i < 6; i++) NeuralFixture.Near(PredatorInputReference.Inputs[i], inputs[i]);
        var expected = PredatorInputReference.Outputs();
        var exactOutputs = new double[2]; brain.Forward(inputs, exactOutputs);
        var random = simulation.RandomState; simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(TargetKind.Prey, sample.TargetKind);
        NeuralFixture.Near(expected[0], sample.NeuralOutputSteering!.Value);
        NeuralFixture.Near(expected[1], sample.NeuralOutputSpeed!.Value);
        Assert.Equal(exactOutputs[0], sample.NeuralOutputSteering); Assert.Equal(exactOutputs[1], sample.NeuralOutputSpeed);
        Assert.Equal(exactOutputs[0], sample.AppliedRotationRadians); // rate 2 * dt .5 = 1
        Assert.Equal(10 * (1 + 0.5 * exactOutputs[1]), sample.EffectiveSpeed);
        Assert.Equal(59, simulation.State.World.Organisms[0].Energy); Assert.Equal(random, simulation.RandomState);
        Assert.Equal((6, 8, 2, 74), (NeuralGenome.InputCount, NeuralGenome.HiddenCount, NeuralGenome.OutputCount, NeuralGenome.WeightCount));
    }

    [Theory]
    [InlineData(60, 40, 30)]
    [InlineData(40, 40, 30)]
    [InlineData(50, 40, 30)]
    [InlineData(50, 40, 0)]
    public void IdenticalFoodAndPreyGeometryProducesBitIdenticalSixChannels(double x, double y, double radius)
    {
        var herbivore = PursuitFixture.Organism(Species.Herbivore, sensing: radius, speed: 10);
        var predator = PursuitFixture.Organism(sensing: radius, speed: 10);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: x, y: y);
        var settings = NeuralFixture.Settings();
        var oldInputs = NeuralFixture.Inputs(herbivore, [new Food(0, x, y, 20)], settings);
        var newInputs = PursuitFixture.Inputs(predator, [predator, prey], settings);
        Assert.Equal(oldInputs.Select(BitConverter.DoubleToInt64Bits), newInputs.Select(BitConverter.DoubleToInt64Bits));
        var bearing = x < 50 ? Math.PI : 0;
        double[] independentlyExpected = [radius == 0 ? 0 : Math.Sin(bearing), radius == 0 ? 1 : Math.Cos(bearing),
            radius == 0 ? -1 : x == 50 && y == 40 ? 1 : 1 - 2 * (10.0 / 30), 1, -0.9, -1];
        Assert.Equal(independentlyExpected.Select(BitConverter.DoubleToInt64Bits), oldInputs.Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoTargetAndCoincidenceRetainHistoricalConventions(bool coincidence)
    {
        var predator = PursuitFixture.Organism(heading: 1.2, speed: 10);
        Organism[] previous = coincidence ? [predator, PursuitFixture.Organism(Species.Herbivore, id: 43)] : [predator];
        var inputs = PursuitFixture.Inputs(predator, previous, NeuralFixture.Settings(), [new Food(0, 50, 40, 20)]);
        Assert.Equal(new[] { 0.0, 1.0, coincidence ? 1.0 : -1.0, 1.0, -0.9, -1.0 }, inputs);
    }

    [Fact]
    public void HerbivoreInputsAndTelemetryRespectEarlierFoodConsumption()
    {
        var first = PursuitFixture.Organism(Species.Herbivore);
        var second = PursuitFixture.Organism(Species.Herbivore, id: 43, brain: NeuralFixture.ChannelBrain(2));
        var simulation = PursuitFixture.Create([first, second], [new Food(0, 50, 40, 20)], neural: true, pickup: 0);
        simulation.EnableDiagnosticsFor(43); simulation.AdvanceTick();
        Assert.Equal(TargetKind.None, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(Math.Tanh(Math.Tanh(-1)), simulation.LastDiagnosticSample.NeuralOutputSteering);
        Assert.Empty(simulation.State.World.FoodItems);
        var bits = NeuralFixture.Inputs(second, [new Food(0, 50, 40, 20)], simulation.Settings, consumed: [true]);
        Assert.Equal(new[] { 0.0, 1.0, -1.0, 1.0, -1.0, -1.0 }, bits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NeuralPursuitIncludesPreyDyingDuringTheTick(bool preyFirst)
    {
        var predator = PursuitFixture.Organism(id: preyFirst ? 43 : 7, steering: 0.5, brain: NeuralFixture.ChannelBrain(0));
        var prey = PursuitFixture.Organism(Species.Herbivore, id: preyFirst ? 7 : 43, y: 50, energy: 0.5, metabolism: 1);
        var simulation = PursuitFixture.Create(new[] { predator, prey }.OrderBy(o => o.Id).ToArray(), neural: true);
        simulation.EnableDiagnosticsFor(predator.Id); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        Assert.Equal(TargetKind.Prey, sample.TargetKind); Assert.Equal(Math.Tanh(Math.Tanh(1)), sample.NeuralOutputSteering);
        Assert.Equal(sample.NeuralOutputSteering * 0.5, sample.AppliedRotationRadians);
        Assert.Single(simulation.State.World.Organisms);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtremeFiniteCoordinatesAndTinyRadiiProduceValidChannelsAndTicks(bool tiny)
    {
        var dimension = tiny ? 1e-310 : double.MaxValue;
        var predator = PursuitFixture.Organism(x: tiny ? 0 : 8e307, y: tiny ? 0 : 8e307,
            sensing: tiny ? double.Epsilon : double.MaxValue);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: tiny ? double.Epsilon : 9e307, y: tiny ? 0 : 8e307);
        var simulation = PursuitFixture.Create([predator, prey], neural: true, width: dimension, height: dimension);
        var inputs = PursuitFixture.Inputs(predator, simulation.State.World.Organisms, simulation.Settings);
        Assert.All(inputs, input => Assert.True(double.IsFinite(input) && input >= -1 && input <= 1));
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind); Assert.Equal(1, simulation.TickCount);
        if (tiny) Assert.Equal(new[] { 0.0, 1.0, -1.0, 1.0, -1.0, 1.0 }, inputs);
    }

    [Fact]
    public void UnderflowedProximityRatioDoesNotTurnPositivePreyDistanceIntoCoincidence()
    {
        var predator = PursuitFixture.Organism(x: 0, y: 0, sensing: double.MaxValue, brain: NeuralFixture.ChannelBrain(0));
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 0, y: double.Epsilon);
        var simulation = PursuitFixture.Create([predator, prey], neural: true);
        var inputs = PursuitFixture.Inputs(predator, simulation.State.World.Organisms, simulation.Settings);
        Assert.Equal(1, inputs[0]); Assert.Equal(Math.Cos(Math.PI / 2), inputs[1]); Assert.Equal(1, inputs[2]);
        simulation.EnableDiagnosticsFor(7); simulation.AdvanceTick();
        Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        Assert.Equal(Math.Tanh(Math.Tanh(1)), simulation.LastDiagnosticSample.NeuralOutputSteering);
    }
}
