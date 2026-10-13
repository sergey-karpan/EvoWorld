using EvoWorld.Core;

namespace EvoWorld.Tests;

internal static class SteeringFixture
{
    internal static SimulationSettings Settings(int organisms = 1, int food = 0, double timestep = 1,
        double width = 100, double height = 80, double sensing = 30, double steering = 2,
        double oldSpeedVariation = 0, double oldMetabolismVariation = 0,
        double sensingVariation = 0, double steeringVariation = 0, double minSensing = 0, double maxSensing = 100,
        double minSteering = 0, double maxSteering = 5, double sensingMutation = 2, double steeringMutation = 0.1,
        int cap = 0, int foodCap = 0, double pickup = 0) =>
        new(42, timestep, width, height, organisms, food, 0, 0, pickup, 1, foodCap, 150, 50, 50, cap,
            oldSpeedVariation, oldMetabolismVariation, 0, 200, 0, 5, 1, 0.05,
            sensing, steering, sensingVariation, steeringVariation, minSensing, maxSensing, minSteering, maxSteering, sensingMutation, steeringMutation);

    internal static Simulation Create(Organism[] organisms, Food[] food, SimulationSettings? settings = null)
    {
        settings ??= Settings(organisms.Length, food.Length);
        return new Simulation(settings, new WorldState(settings.WorldWidth, settings.WorldHeight, organisms, food));
    }

    internal static Organism Organism(double x = 10, double y = 10, double heading = 0, double sensing = 30,
        double steering = 2, double speed = 0, double metabolism = 0, double energy = 100, int id = 0) =>
        new(id, x, y, heading, energy, speed, metabolism, sensing, steering);
}
