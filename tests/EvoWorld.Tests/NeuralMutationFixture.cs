using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class NeuralMutationFixture
{
    internal static SimulationSettings Settings(double mutation = 0.125, bool enabled = true, int organisms = 1, int food = 0,
        int cap = 10, int foodCap = 0, double initialAmplitude = 0, double lower = -1, double upper = 1,
        double duration = 1, double pickup = 0, double cost = 50, double childEnergy = 50, double threshold = 150,
        double interval = 1, bool traitMutation = false) =>
        new(42, duration, 100, 80, organisms, food, 0, 0, pickup, interval, foodCap, threshold, cost, childEnergy, cap,
            0, 0, 0, 200, 0, 5, traitMutation ? 1 : 0, traitMutation ? 0.05 : 0,
            30, 0, 0, 0, 0, 100, 0, 5, traitMutation ? 2 : 0, traitMutation ? 0.1 : 0,
            enabled, lower, upper, initialAmplitude, 0.5, mutation);

    internal static Organism Parent(NeuralGenome? brain = null, int id = 0, double energy = 160,
        double x = 50, double y = 40, double heading = 0.25) =>
        new(id, x, y, heading, energy, 0, 0, 30, 0, brain ?? NeuralGenome.CreateZero());

    internal static Simulation Create(Organism[] parents, SimulationSettings? settings = null, Food[]? food = null,
        long? nextOrganismId = null)
    {
        food ??= []; settings ??= Settings(organisms: parents.Length, food: food.Length);
        return new Simulation(settings, new WorldState(100, 80, parents, food), nextOrganismId: nextOrganismId);
    }

    internal static double[] MutatedWeights(NeuralGenome parent, double amplitude, double lower, double upper,
        EvoWorld.Core.Random.Pcg32 reference)
    {
        // Independent test oracle for small, non-overflowing candidates; does not call NeuralMutation.
        return parent.Weights.ToArray().Select(w => Math.Clamp(w + (2 * (reference.NextUInt() / 4294967296.0) - 1) * amplitude, lower, upper)).ToArray();
    }

    internal static void AssertEntity(double[] expected, Organism actual)
    {
        double[] values = [actual.X, actual.Y, actual.HeadingRadians, actual.Speed, actual.Metabolism, actual.SensingRadius, actual.SteeringRate];
        for (var i = 0; i < values.Length; i++) NeuralFixture.Near(expected[i], values[i]);
    }

    internal static void AssertBits(double[] expected, NeuralGenome actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Weights.ToArray().Select(BitConverter.DoubleToInt64Bits));
}
