using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public class ExperimentSettingsTests
{
    public static IEnumerable<object[]> AllSelections()
    {
        foreach (var mode in new[] { 0, 1 })
            foreach (var amplitude in new[] { 0.0, 0.05, 0.1, 0.5 }) yield return [mode, amplitude];
    }

    [Theory]
    [MemberData(nameof(AllSelections))]
    public void EveryPublicSettingMatchesIndependentExpectedDefaultsForEverySelection(int mode, double amplitude)
    {
        var actual = ExperimentSettings.Create(new((ControllerMode)mode, amplitude));
        var expected = ExpectedDefaults();
        expected[nameof(SimulationSettings.EnableNeuralControl)] = mode == 1;
        expected[nameof(SimulationSettings.NeuralWeightMutationAmplitude)] = mode == 1 ? amplitude : 0.0;
        var properties = typeof(SimulationSettings).GetProperties();
        Assert.Equal(expected.Keys.Order(), properties.Select(p => p.Name).Order());
        foreach (var property in properties) Assert.Equal(expected[property.Name], property.GetValue(actual));

        // Independent literal values above are the primary oracle. Cross-check
        // Core's current defaults as well, especially the live initialization amplitude.
        var baseline = SimulationSettings.CreateDefault();
        Assert.True(baseline.InitialNeuralWeightVariationAmplitude > 0);
        Assert.Equal(baseline.InitialNeuralWeightVariationAmplitude, actual.InitialNeuralWeightVariationAmplitude);
        foreach (var property in properties.Where(p => p.Name is not nameof(SimulationSettings.EnableNeuralControl)
            and not nameof(SimulationSettings.NeuralWeightMutationAmplitude)))
            Assert.Equal(property.GetValue(baseline), property.GetValue(actual));
    }

    [Fact]
    public void InitialSelectionIsRuleBasedWithPointOneReadyForNeuralDemonstration()
    {
        Assert.Equal(ControllerMode.RuleBased, ExperimentOptions.Initial.Controller);
        Assert.Equal(0.1, ExperimentOptions.Initial.MutationAmplitude);
        Assert.Equal(new[] { 0.0, 0.05, 0.1, 0.5 }, ExperimentSettings.MutationChoices.ToArray());
        var settings = ExperimentSettings.Create(ExperimentOptions.Initial);
        Assert.False(settings.EnableNeuralControl); Assert.Equal(0, settings.NeuralWeightMutationAmplitude);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0.2)]
    public void UnsupportedChoicesAreRejectedInsteadOfSilentlyMapped(double amplitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentSettings.Create(new(ControllerMode.Neural, amplitude)));
    }

    [Fact]
    public void UnknownControllerIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExperimentSettings.Create(new((ControllerMode)99, 0.1)));
    }

    [Fact]
    public void RuleBasedMappingLeavesCoreDefaultsAndInitialWorldUnchanged()
    {
        var defaults = SimulationSettings.CreateDefault(); var defaultValues = ExpectedDefaults();
        var mapped = new Simulation(ExperimentSettings.Create(new(ControllerMode.RuleBased, 0.5)));
        var original = new Simulation(defaults); NeuralFixture.SameState(original, mapped);
        foreach (var property in typeof(SimulationSettings).GetProperties())
            Assert.Equal(defaultValues[property.Name], property.GetValue(SimulationSettings.CreateDefault()));
        for (var tick = 0; tick < 100; tick++)
        {
            mapped.AdvanceTick(); original.AdvanceTick(); NeuralFixture.SameState(original, mapped);
        }
    }

    [Fact]
    public void NeuralModePreservesPlacementAndUsesNonzeroInitialGenomesEvenAtZeroMutation()
    {
        var baseline = new Simulation(SimulationSettings.CreateDefault());
        var neural = new Simulation(ExperimentSettings.Create(new(ControllerMode.Neural, 0)));
        Assert.True(neural.Settings.EnableNeuralControl); Assert.Equal(0, neural.Settings.NeuralWeightMutationAmplitude);
        Assert.Equal(baseline.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians)),
            neural.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians)));
        Assert.Equal(baseline.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)),
            neural.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
        Assert.All(neural.State.World.Organisms, o => Assert.Contains(o.Brain.Weights.ToArray(), w => w != 0));
        Assert.NotEqual(baseline.RandomState, neural.RandomState); // Extra neural initialization draws are intentional.
    }

    private static Dictionary<string, object> ExpectedDefaults() => new()
    {
        [nameof(SimulationSettings.Seed)] = 42UL,
        [nameof(SimulationSettings.TickDurationSeconds)] = 1.0 / 30.0,
        [nameof(SimulationSettings.WorldWidth)] = 1000.0,
        [nameof(SimulationSettings.WorldHeight)] = 700.0,
        [nameof(SimulationSettings.InitialOrganismCount)] = 20,
        [nameof(SimulationSettings.InitialPredatorCount)] = 0,
        [nameof(SimulationSettings.InitialPredatorEnergy)] = 100.0,
        [nameof(SimulationSettings.PredationEnergyTransferRatio)] = 0.8,
        [nameof(SimulationSettings.InitialFoodCount)] = 100,
        [nameof(SimulationSettings.OrganismSpeed)] = 50.0,
        [nameof(SimulationSettings.OrganismEnergyDrainPerSecond)] = 1.0,
        [nameof(SimulationSettings.FoodPickupRadius)] = 8.0,
        [nameof(SimulationSettings.FoodRegenerationIntervalSeconds)] = 2.0,
        [nameof(SimulationSettings.MaxFoodCount)] = 200,
        [nameof(SimulationSettings.ReproductionEnergyThreshold)] = 150.0,
        [nameof(SimulationSettings.ReproductionEnergyCost)] = 50.0,
        [nameof(SimulationSettings.OffspringInitialEnergy)] = 50.0,
        [nameof(SimulationSettings.MaxOrganismCount)] = 500,
        [nameof(SimulationSettings.InitialSpeedVariationAmplitude)] = 0.0,
        [nameof(SimulationSettings.InitialMetabolismVariationAmplitude)] = 0.0,
        [nameof(SimulationSettings.MinSpeed)] = 0.0,
        [nameof(SimulationSettings.MaxSpeed)] = 200.0,
        [nameof(SimulationSettings.MinMetabolism)] = 0.0,
        [nameof(SimulationSettings.MaxMetabolism)] = 5.0,
        [nameof(SimulationSettings.SpeedMutationAmplitude)] = 1.0,
        [nameof(SimulationSettings.MetabolismMutationAmplitude)] = 0.05,
        [nameof(SimulationSettings.OrganismSensingRadius)] = 30.0,
        [nameof(SimulationSettings.OrganismSteeringRate)] = 2.0,
        [nameof(SimulationSettings.InitialSensingRadiusVariationAmplitude)] = 0.0,
        [nameof(SimulationSettings.InitialSteeringRateVariationAmplitude)] = 0.0,
        [nameof(SimulationSettings.MinSensingRadius)] = 0.0,
        [nameof(SimulationSettings.MaxSensingRadius)] = 100.0,
        [nameof(SimulationSettings.MinSteeringRate)] = 0.0,
        [nameof(SimulationSettings.MaxSteeringRate)] = 5.0,
        [nameof(SimulationSettings.SensingRadiusMutationAmplitude)] = 2.0,
        [nameof(SimulationSettings.SteeringRateMutationAmplitude)] = 0.1,
        [nameof(SimulationSettings.EnableNeuralControl)] = false,
        [nameof(SimulationSettings.NeuralWeightMin)] = -1.0,
        [nameof(SimulationSettings.NeuralWeightMax)] = 1.0,
        [nameof(SimulationSettings.InitialNeuralWeightVariationAmplitude)] = 0.5,
        [nameof(SimulationSettings.SpeedModulationAmplitude)] = 0.5,
        [nameof(SimulationSettings.NeuralWeightMutationAmplitude)] = 0.0
    };
}
