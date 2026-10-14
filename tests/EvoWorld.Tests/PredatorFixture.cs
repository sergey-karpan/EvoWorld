using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class PredatorFixture
{
    internal static SimulationSettings Settings(int herbivores = 1, int predators = 1, int food = 1,
        double energy = 125, bool neural = false, double mutation = 0.125, bool oldVariation = false,
        bool newVariation = false, double initialNeural = 0.5, int cap = 12, int foodCap = 100,
        double duration = 0.1, double width = 100, double height = 80, double metabolism = 1,
        double pickup = 8, double threshold = 150) =>
        new(42, duration, width, height, herbivores, food, 10, metabolism, pickup, 0.2, foodCap,
            threshold, 50, 50, cap, oldVariation ? 10 : 0, oldVariation ? 0.5 : 0, 0, 200, 0, 5, 1, 0.05,
            30, 2, newVariation ? 10 : 0, newVariation ? 0.5 : 0, 0, 100, 0, 5, 2, 0.1,
            neural, -1, 1, initialNeural, 0.5, mutation, predators, energy);

    internal static Organism Organism(Species species, int id = 0, double energy = 160, double x = 50,
        double y = 40, double speed = 0, double metabolism = 0, double heading = 0.25, NeuralGenome? brain = null) =>
        new(id, x, y, heading, energy, speed, metabolism, 30, 2, brain ?? NeuralFixture.Brain(0.2, -0.3), species);

    internal static Simulation Create(SimulationSettings settings, params Organism[] organisms) =>
        new(settings, new WorldState(settings.WorldWidth, settings.WorldHeight, organisms, []));

    internal static void SameSample(OrganismDiagnosticSample? expected, OrganismDiagnosticSample? actual)
    {
        if (expected is null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        Assert.Equal((expected.OrganismId, expected.TickCount, expected.DidAct, expected.IsNeuralMode, expected.IsRuleBasedMode),
            (actual.OrganismId, actual.TickCount, actual.DidAct, actual.IsNeuralMode, actual.IsRuleBasedMode));
        double?[] first = [expected.NeuralOutputSteering, expected.NeuralOutputSpeed, expected.AppliedRotationRadians, expected.EffectiveSpeed];
        double?[] second = [actual.NeuralOutputSteering, actual.NeuralOutputSpeed, actual.AppliedRotationRadians, actual.EffectiveSpeed];
        Assert.Equal(first.Select(v => v is double d ? (long?)BitConverter.DoubleToInt64Bits(d) : null),
            second.Select(v => v is double d ? (long?)BitConverter.DoubleToInt64Bits(d) : null));
    }

    internal static void SameState(Simulation first, Simulation second)
    {
        NeuralFixture.SameState(first, second);
        Assert.Equal(first.State.World.Organisms.Select(o => o.Species), second.State.World.Organisms.Select(o => o.Species));
        Assert.Equal(first.GetStatistics(), second.GetStatistics());
        static IEnumerable<long> Bits(WorldState world)
        {
            yield return BitConverter.DoubleToInt64Bits(world.WorldWidth);
            yield return BitConverter.DoubleToInt64Bits(world.WorldHeight);
            foreach (var o in world.Organisms)
                foreach (var v in new[] { o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate })
                    yield return BitConverter.DoubleToInt64Bits(v);
            foreach (var f in world.FoodItems)
                foreach (var v in new[] { f.X, f.Y, f.Energy }) yield return BitConverter.DoubleToInt64Bits(v);
        }
        Assert.Equal(Bits(first.State.World), Bits(second.State.World));
    }
}
