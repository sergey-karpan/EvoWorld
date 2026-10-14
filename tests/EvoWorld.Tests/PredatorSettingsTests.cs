using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorSettingsTests
{
    [Fact]
    public void HistoricalConstructorSignaturesAndNewDefaultsArePreserved()
    {
        var defaults = SimulationSettings.CreateDefault();
        var constructors = typeof(SimulationSettings).GetConstructors().Where(c => c.GetParameters().Length < 41).ToArray();
        Assert.Equal(new[] { 4, 6, 7, 9, 11, 15, 23, 33, 38, 39 }, constructors.Select(c => c.GetParameters().Length).Order());
        foreach (var constructor in constructors)
        {
            var values = constructor.GetParameters().Select(p => typeof(SimulationSettings)
                .GetProperty(char.ToUpperInvariant(p.Name![0]) + p.Name[1..])!.GetValue(defaults)).ToArray();
            var settings = (SimulationSettings)constructor.Invoke(values);
            Assert.Equal(0, settings.InitialPredatorCount); Assert.Equal(100, settings.InitialPredatorEnergy);
        }
        Assert.Null(typeof(SimulationSettings).GetProperty(nameof(SimulationSettings.InitialPredatorCount))!.SetMethod);
        Assert.Null(typeof(SimulationSettings).GetProperty(nameof(SimulationSettings.InitialPredatorEnergy))!.SetMethod);
        Assert.Single(typeof(SimulationSettings).GetConstructors(), c => c.GetParameters().Length == 41);
    }

    [Fact]
    public void FortyOneArgumentsMapEveryPublicPropertyToIndependentExpectedValues()
    {
        var actual = PredatorFixture.Settings(predators: 3, energy: 175, neural: true, oldVariation: true, newVariation: true);
        Dictionary<string, object> expected = new()
        {
            ["Seed"] = 42UL, ["TickDurationSeconds"] = 0.1, ["WorldWidth"] = 100.0, ["WorldHeight"] = 80.0,
            ["InitialOrganismCount"] = 1, ["InitialFoodCount"] = 1, ["OrganismSpeed"] = 10.0,
            ["OrganismEnergyDrainPerSecond"] = 1.0, ["FoodPickupRadius"] = 8.0,
            ["FoodRegenerationIntervalSeconds"] = 0.2, ["MaxFoodCount"] = 100,
            ["ReproductionEnergyThreshold"] = 150.0, ["ReproductionEnergyCost"] = 50.0,
            ["OffspringInitialEnergy"] = 50.0, ["MaxOrganismCount"] = 12,
            ["InitialSpeedVariationAmplitude"] = 10.0, ["InitialMetabolismVariationAmplitude"] = 0.5,
            ["MinSpeed"] = 0.0, ["MaxSpeed"] = 200.0, ["MinMetabolism"] = 0.0, ["MaxMetabolism"] = 5.0,
            ["SpeedMutationAmplitude"] = 1.0, ["MetabolismMutationAmplitude"] = 0.05,
            ["OrganismSensingRadius"] = 30.0, ["OrganismSteeringRate"] = 2.0,
            ["InitialSensingRadiusVariationAmplitude"] = 10.0, ["InitialSteeringRateVariationAmplitude"] = 0.5,
            ["MinSensingRadius"] = 0.0, ["MaxSensingRadius"] = 100.0, ["MinSteeringRate"] = 0.0, ["MaxSteeringRate"] = 5.0,
            ["SensingRadiusMutationAmplitude"] = 2.0, ["SteeringRateMutationAmplitude"] = 0.1,
            ["EnableNeuralControl"] = true, ["NeuralWeightMin"] = -1.0, ["NeuralWeightMax"] = 1.0,
            ["InitialNeuralWeightVariationAmplitude"] = 0.5, ["SpeedModulationAmplitude"] = 0.5,
            ["NeuralWeightMutationAmplitude"] = 0.125, ["InitialPredatorCount"] = 3, ["InitialPredatorEnergy"] = 175.0
        };
        Assert.Equal(expected.Keys.Order(), typeof(SimulationSettings).GetProperties().Select(p => p.Name).Order());
        foreach (var property in typeof(SimulationSettings).GetProperties()) Assert.Equal(expected[property.Name], property.GetValue(actual));
        var reference = new SimulationSettings(42, 0.1, 100, 80, 1, 1, 10, 1, 8, 0.2, 100,
            150, 50, 50, 12, 10, 0.5, 0, 200, 0, 5, 1, 0.05, 30, 2, 10, 0.5, 0, 100, 0, 5, 2, 0.1, true, -1, 1, 0.5, 0.5, 0.125);
        foreach (var property in typeof(SimulationSettings).GetProperties().Where(p => p.Name is not "InitialPredatorCount" and not "InitialPredatorEnergy"))
            Assert.Equal(property.GetValue(reference), property.GetValue(actual));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidEnergyIsRejectedEvenWithNoPredators(double energy) =>
        Assert.Equal("initialPredatorEnergy", Assert.Throws<ArgumentOutOfRangeException>(() => PredatorFixture.Settings(predators: 0, energy: energy)).ParamName);

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(100)]
    [InlineData(double.MaxValue)]
    public void EveryFinitePositiveEnergyIsAccepted(double energy) => Assert.Equal(energy, PredatorFixture.Settings(energy: energy).InitialPredatorEnergy);

    [Fact]
    public void NegativeAndOverflowingCountsAreRejectedBeforeAllocation()
    {
        Assert.Equal("initialPredatorCount", Assert.Throws<ArgumentOutOfRangeException>(() => PredatorFixture.Settings(predators: -1)).ParamName);
        Assert.Equal("initialPredatorCount", Assert.Throws<ArgumentOutOfRangeException>(() => PredatorFixture.Settings(herbivores: int.MaxValue, predators: 1)).ParamName);
        Assert.Equal("initialPredatorCount", Assert.Throws<ArgumentOutOfRangeException>(() => PredatorFixture.Settings(herbivores: int.MaxValue, predators: int.MaxValue)).ParamName);
        // Historical settings accepted Int32.MaxValue counts without allocating.
        // Preserve that signature contract; enforce the actual runtime array limit
        // immediately before initialization allocation (no partial draws).
        var settings = PredatorFixture.Settings(herbivores: Array.MaxLength, predators: 1);
        var random = new EvoWorld.Core.Random.Pcg32(42); var state = random.CaptureState();
        Assert.Equal("settings", Assert.Throws<ArgumentOutOfRangeException>(() => WorldInitializer.Create(settings, random)).ParamName);
        Assert.Equal(state, random.CaptureState());
    }
}
