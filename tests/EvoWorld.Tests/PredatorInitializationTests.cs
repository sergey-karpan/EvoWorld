using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorInitializationTests
{
    public static IEnumerable<object[]> Modes()
    {
        foreach (var old in new[] { false, true })
            foreach (var recent in new[] { false, true })
                foreach (var neural in new[] { false, true }) yield return [old, recent, neural];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void AllEightPhaseModesPreserveSpeciesAndMatchUniformDrawContract(bool old, bool recent, bool neural)
    {
        var settings = PredatorFixture.Settings(oldVariation: old, newVariation: recent, neural: neural);
        var mixed = new Simulation(settings);
        var flat = new Simulation(PredatorFixture.Settings(herbivores: 2, predators: 0, oldVariation: old, newVariation: recent, neural: neural));
        var organisms = mixed.State.World.Organisms;
        Assert.Equal(2, organisms.Count); Assert.Equal(Species.Herbivore, organisms[0].Species); Assert.Equal(Species.Predator, organisms[1].Species);
        Assert.Equal(new[] { 0, 1 }, organisms.Select(o => o.Id));
        Assert.Equal(100, organisms[0].Energy); Assert.Equal(125, organisms[1].Energy);
        Assert.Equal(flat.State.World.Organisms.Select(o => (o.X, o.Y, o.HeadingRadians, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)),
            organisms.Select(o => (o.X, o.Y, o.HeadingRadians, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)));
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(flat.State.World.Organisms[i].Brain.Weights.ToArray(), organisms[i].Brain.Weights.ToArray());
            Assert.InRange(organisms[i].X, 0, Math.BitDecrement(100)); Assert.InRange(organisms[i].Y, 0, Math.BitDecrement(80));
            if (!old) { Assert.Equal(10, organisms[i].Speed); Assert.Equal(1, organisms[i].Metabolism); }
            if (!recent) { Assert.Equal(30, organisms[i].SensingRadius); Assert.Equal(2, organisms[i].SteeringRate); }
        }
        Assert.Equal(flat.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y)), mixed.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y)));
        var draws = 8 + (old ? 4 : 0) + (recent ? 4 : 0) + (neural ? 148 : 0);
        var reference = new Pcg32(42); for (var i = 0; i < draws; i++) reference.NextUInt();
        Assert.Equal(reference.CaptureState(), mixed.RandomState);
        Assert.Equal(flat.RandomState, mixed.RandomState);
        Assert.Equal(2, mixed.NextOrganismId);
    }

    [Fact]
    public void IndependentMixedGoldenChecksEveryCoordinateTraitWeightAndFinalState()
    {
        var simulation = new Simulation(PredatorFixture.Settings(neural: true, oldVariation: true, newVariation: true));
        for (var i = 0; i < 2; i++)
        {
            var o = simulation.State.World.Organisms[i];
            double[] actual = [o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate];
            Assert.Equal(PredatorInitializationGolden.Organisms[i].Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
            NeuralMutationFixture.AssertBits(PredatorInitializationGolden.Brains[i], o.Brain);
            Assert.Equal(i == 0 ? Species.Herbivore : Species.Predator, o.Species);
        }
        var food = Assert.Single(simulation.State.World.FoodItems);
        Assert.Equal(PredatorInitializationGolden.Food, new[] { food.X, food.Y, food.Energy });
        Assert.Equal(new Pcg32State(PredatorInitializationGolden.FinalState, 3), simulation.RandomState);
        Assert.Equal(164, PredatorInitializationGolden.Draws);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 3)]
    public void PlacementOrderIsHerbivoresPredatorsThenFood(int herbivores, int predators)
    {
        var simulation = new Simulation(PredatorFixture.Settings(herbivores, predators, food: 2, neural: false));
        var reference = new Pcg32(42);
        foreach (var o in simulation.State.World.Organisms)
        {
            Assert.Equal(reference.NextUInt() / 4294967296.0 * 100, o.X);
            Assert.Equal(reference.NextUInt() / 4294967296.0 * 80, o.Y);
            Assert.Equal(reference.NextUInt() / 4294967296.0 * (2 * Math.PI), o.HeadingRadians);
            Assert.Equal(o.Id < herbivores ? Species.Herbivore : Species.Predator, o.Species);
        }
        foreach (var f in simulation.State.World.FoodItems)
        {
            Assert.Equal(reference.NextUInt() / 4294967296.0 * 100, f.X);
            Assert.Equal(reference.NextUInt() / 4294967296.0 * 80, f.Y);
        }
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
        Assert.Equal(herbivores + predators, simulation.NextOrganismId);
    }

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    public void PredatorCoordinatesAndEnergyUseExistingExtremeFiniteSafety(double dimension)
    {
        var simulation = new Simulation(PredatorFixture.Settings(width: dimension, height: dimension, energy: dimension));
        Assert.All(simulation.State.World.Organisms, o => { Assert.True(o.X >= 0 && o.X < dimension); Assert.True(o.Y >= 0 && o.Y < dimension); });
        Assert.Equal(dimension, simulation.State.World.Organisms[1].Energy);
        Assert.Equal(100, simulation.State.World.Organisms[0].Energy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroNeuralAmplitudeSharesTheHistoricalZeroBrainWithoutExtraDraws(bool neural)
    {
        var simulation = new Simulation(PredatorFixture.Settings(neural: neural, initialNeural: 0));
        Assert.All(simulation.State.World.Organisms, o => Assert.Same(Organism.ZeroBrain, o.Brain));
        var reference = new Pcg32(42); for (var i = 0; i < 8; i++) reference.NextUInt();
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }
}
