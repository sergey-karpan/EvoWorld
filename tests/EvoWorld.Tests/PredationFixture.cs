using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class PredationFixture
{
    internal static SimulationSettings Settings(bool neural = false, double ratio = 0.8, double radius = 8,
        double duration = 1, double width = 100, double height = 80, int cap = 0, int foodCap = 0,
        int herbivores = 3, int predators = 2, int food = 0, double mutation = 0,
        double initialNeural = 0) =>
        new(42, duration, width, height, herbivores, food, 10, 1, radius, 1, foodCap,
            150, 50, 50, cap, 0, 0, 0, 200, 0, 5, 1, 0.05,
            30, 2, 0, 0, 0, 100, 0, 5, 2, 0.1,
            neural, -1, 1, initialNeural, 0.5, mutation, predators, 100, ratio);

    internal static Simulation Create(Organism[] organisms, Food[]? food = null, bool neural = false,
        double ratio = 0.8, double radius = 8, double duration = 1, double width = 100,
        double height = 80, int cap = 0, int foodCap = 0, long predations = 0,
        long? nextOrganismId = null, double mutation = 0)
    {
        food ??= [];
        var settings = Settings(neural, ratio, radius, duration, width, height, cap, foodCap,
            organisms.Count(o => o.Species == Species.Herbivore), organisms.Count(o => o.Species == Species.Predator),
            food.Length, mutation);
        return new(settings, new WorldState(width, height, organisms.OrderBy(o => o.Id), food),
            0, null, nextOrganismId, predations);
    }

    internal static Organism Predator(int id = 7, double x = 50, double y = 40, double energy = 100,
        double speed = 0, double metabolism = 0, double sensing = 0, double heading = 0,
        double steering = 0, NeuralGenome? brain = null) =>
        PursuitFixture.Organism(Species.Predator, id, x, y, heading, speed, metabolism, energy, sensing, steering, brain);

    internal static Organism Prey(int id = 43, double x = 50, double y = 40, double energy = 75,
        double speed = 0, double metabolism = 0, double sensing = 0, double heading = 0,
        double steering = 0, NeuralGenome? brain = null) =>
        PursuitFixture.Organism(Species.Herbivore, id, x, y, heading, speed, metabolism, energy, sensing, steering, brain);

    internal static void Same(Simulation expected, Simulation actual)
    {
        PredatorFixture.SameState(expected, actual); // Full world, genomes, RNG, IDs, species, statistics.
        var e = expected.LastDiagnosticSample;
        var a = actual.LastDiagnosticSample;
        PredatorFixture.SameSample(e, a);
        Assert.Equal(e?.PreyConsumedId, a?.PreyConsumedId);
        Assert.Equal(e?.EnergyGainedFromPrey is double gain ? (long?)BitConverter.DoubleToInt64Bits(gain) : null,
            a?.EnergyGainedFromPrey is double observed ? (long?)BitConverter.DoubleToInt64Bits(observed) : null);
    }
}
