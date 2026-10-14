using EvoWorld.Core;

namespace EvoWorld.Tests;

internal static class PursuitFixture
{
    internal static Organism Organism(Species species = Species.Predator, int id = 7, double x = 50, double y = 40,
        double heading = 0, double speed = 0, double metabolism = 0, double energy = 100,
        double sensing = 30, double steering = 2, NeuralGenome? brain = null) =>
        new(id, x, y, heading, energy, speed, metabolism, sensing, steering, brain ?? EvoWorld.Core.Organism.ZeroBrain, species);

    internal static Simulation Create(Organism[] organisms, Food[]? food = null, bool neural = false,
        double duration = 1, double pickup = 8, int cap = 0, int foodCap = 0, double width = 100, double height = 80)
    {
        food ??= [];
        var settings = NeuralFixture.Settings(enabled: neural, organisms: organisms.Length, food: food.Length,
            duration: duration, width: width, height: height, amplitude: 0, pickup: pickup, cap: cap, foodCap: foodCap);
        return new Simulation(settings, new WorldState(width, height, organisms, food));
    }

    internal static double[] Inputs(Organism organism, IReadOnlyList<Organism> previous, SimulationSettings settings,
        Food[]? food = null, bool[]? consumed = null, double? energy = null)
    {
        food ??= []; consumed ??= new bool[food.Length];
        var target = TargetSensing.Resolve(organism, food, consumed, previous);
        var inputs = new double[6];
        NeuralControl.EncodeInputs(organism, energy ?? organism.Energy, target, settings, inputs);
        return inputs;
    }
}
