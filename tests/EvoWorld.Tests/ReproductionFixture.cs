using EvoWorld.Core;

namespace EvoWorld.Tests;

internal static class ReproductionFixture
{
    internal static Simulation Create(Organism[] organisms, Food[]? food = null, double threshold = 150,
        double cost = 50, double childEnergy = 50, int cap = 10, double drain = 0, double speed = 0,
        double radius = 0, int foodCap = 0, double interval = 1, double width = 100, double height = 80,
        long? nextOrganismId = null, long? nextFoodId = null, long tick = 0, int? initialOrganisms = null)
    {
        food ??= [];
        var settings = new SimulationSettings(42, 1, width, height, initialOrganisms ?? organisms.Length,
            food.Length, speed, drain, radius, interval, foodCap, threshold, cost, childEnergy, cap);
        return new Simulation(settings, new WorldState(width, height, organisms, food), tick, nextFoodId, nextOrganismId);
    }
}
