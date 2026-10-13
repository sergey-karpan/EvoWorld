using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class NeuralFixture
{
    internal static SimulationSettings Settings(bool enabled = true, int organisms = 1, int food = 0,
        double duration = 1, double width = 100, double height = 80, double weightMin = -1, double weightMax = 1,
        double amplitude = 0.5, double modulation = 0.5, double oldSpeedVariation = 0, double oldMetabolismVariation = 0,
        double sensingVariation = 0, double steeringVariation = 0, int cap = 0, int foodCap = 0,
        double pickup = 0, double minSpeed = 0, double maxSpeed = 200, double baselineSpeed = 10,
        double metabolism = 1, double sensing = 30, double steering = 2, double interval = 1,
        double speedMutation = 1, double metabolismMutation = 0.05, double sensingMutation = 2, double steeringMutation = 0.1) =>
        new(42, duration, width, height, organisms, food, baselineSpeed, metabolism, pickup, interval, foodCap,
            150, 50, 50, cap, oldSpeedVariation, oldMetabolismVariation, minSpeed, maxSpeed, 0, 5,
            speedMutation, metabolismMutation, sensing, steering, sensingVariation, steeringVariation, 0, 100, 0, 5,
            sensingMutation, steeringMutation, enabled, weightMin, weightMax, amplitude, modulation);

    internal static NeuralGenome Brain(double steeringBias = 0, double speedBias = 0)
    {
        var weights = new double[74]; weights[72] = steeringBias; weights[73] = speedBias;
        return new NeuralGenome(weights);
    }

    internal static NeuralGenome ChannelBrain(int input, int output = 0)
    {
        var weights = new double[74]; weights[input] = 1; weights[56 + output * 8] = 1;
        return new NeuralGenome(weights);
    }

    internal static Organism Organism(NeuralGenome? brain = null, int id = 0, double x = 50, double y = 40,
        double heading = 0.25, double speed = 10, double metabolism = 1, double energy = 100, double sensing = 30, double steering = 2) =>
        new(id, x, y, heading, energy, speed, metabolism, sensing, steering, brain ?? NeuralGenome.CreateZero());

    internal static Simulation Create(Organism[] organisms, Food[]? food = null, SimulationSettings? settings = null)
    {
        food ??= []; settings ??= Settings(organisms: organisms.Length, food: food.Length);
        return new Simulation(settings, new WorldState(settings.WorldWidth, settings.WorldHeight, organisms, food));
    }

    internal static double[] Inputs(Organism organism, Food[]? food = null, SimulationSettings? settings = null,
        double? energy = null, bool[]? consumed = null)
    {
        food ??= []; settings ??= Settings(); consumed ??= new bool[food.Length];
        var inputs = new double[6];
        NeuralControl.EncodeInputs(organism, energy ?? organism.Energy, food, consumed, settings, inputs);
        return inputs;
    }

    internal static void Near(double expected, double actual, double tolerance = 2e-14)
    {
        Assert.True(double.IsFinite(actual)); Assert.InRange(Math.Abs(actual - expected), 0, tolerance);
    }

    internal static void SameState(Simulation first, Simulation second)
    {
        StatisticsFixture.AssertWorlds(first, second);
        StatisticsFixture.AssertBits(first.GetStatistics(), second.GetStatistics());
        for (var i = 0; i < first.State.World.Organisms.Count; i++)
            Assert.Equal(first.State.World.Organisms[i].Brain.Weights.ToArray().Select(BitConverter.DoubleToInt64Bits),
                second.State.World.Organisms[i].Brain.Weights.ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    internal static void Rollback<T>(Simulation simulation) where T : Exception
    {
        var state = simulation.State; var random = simulation.RandomState;
        var foodId = simulation.NextFoodId; var organismId = simulation.NextOrganismId;
        var statistics = simulation.GetStatistics();
        var field = typeof(Simulation).GetField("_advancement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var advancement = field.GetValue(simulation);
        var brains = state.World.Organisms.Select(o => (o.Brain, o.Brain.Weights.ToArray())).ToArray();
        Assert.Throws<T>(simulation.AdvanceTick);
        Assert.Same(advancement, field.GetValue(simulation)); Assert.Same(state, simulation.State);
        Assert.Same(state.World, simulation.State.World); Assert.Same(state.World.Organisms, simulation.State.World.Organisms);
        Assert.Same(state.World.FoodItems, simulation.State.World.FoodItems);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(foodId, simulation.NextFoodId); Assert.Equal(organismId, simulation.NextOrganismId);
        StatisticsFixture.AssertBits(statistics, simulation.GetStatistics());
        for (var i = 0; i < brains.Length; i++)
        {
            Assert.Same(brains[i].Brain, simulation.State.World.Organisms[i].Brain);
            Assert.Equal(brains[i].Item2, simulation.State.World.Organisms[i].Brain.Weights.ToArray());
        }
    }
}
