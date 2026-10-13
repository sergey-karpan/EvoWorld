using EvoWorld.Core;

namespace EvoWorld.Tests;

internal static class TraitFixture
{
    internal static SimulationSettings Settings(int organisms = 1, int food = 0, double speed = 50, double metabolism = 1,
        double initialSpeedAmplitude = 0, double initialMetabolismAmplitude = 0,
        double minSpeed = 0, double maxSpeed = 200, double minMetabolism = 0, double maxMetabolism = 5,
        double speedAmplitude = 1, double metabolismAmplitude = 0.05, int cap = 10, int foodCap = 0,
        double duration = 1, double radius = 0) =>
        new(42, duration, 100, 80, organisms, food, speed, metabolism, radius, 1, foodCap, 150, 50, 50, cap,
            initialSpeedAmplitude, initialMetabolismAmplitude, minSpeed, maxSpeed, minMetabolism, maxMetabolism, speedAmplitude, metabolismAmplitude);

    internal static Simulation Create(Organism[] organisms, Food[]? food = null, SimulationSettings? settings = null)
    {
        food ??= []; settings ??= Settings(organisms.Length, food.Length);
        return new Simulation(settings, new WorldState(100, 80, organisms, food));
    }
}
