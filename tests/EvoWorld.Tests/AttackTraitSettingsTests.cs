using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

internal static class AttackSettings
{
    internal static SimulationSettings Copy(SimulationSettings s, double radius = 8, double efficiency = 1,
        double minRadius = 0, double maxRadius = 50, double minEfficiency = 0, double maxEfficiency = 1,
        double initialRadius = 0, double initialEfficiency = 0, double radiusMutation = 1, double efficiencyMutation = 0.05) =>
        new(s.Seed, s.TickDurationSeconds, s.WorldWidth, s.WorldHeight, s.InitialOrganismCount, s.InitialFoodCount,
            s.OrganismSpeed, s.OrganismEnergyDrainPerSecond, s.FoodPickupRadius, s.FoodRegenerationIntervalSeconds,
            s.MaxFoodCount, s.ReproductionEnergyThreshold, s.ReproductionEnergyCost, s.OffspringInitialEnergy, s.MaxOrganismCount,
            s.InitialSpeedVariationAmplitude, s.InitialMetabolismVariationAmplitude, s.MinSpeed, s.MaxSpeed, s.MinMetabolism,
            s.MaxMetabolism, s.SpeedMutationAmplitude, s.MetabolismMutationAmplitude, s.OrganismSensingRadius, s.OrganismSteeringRate,
            s.InitialSensingRadiusVariationAmplitude, s.InitialSteeringRateVariationAmplitude, s.MinSensingRadius, s.MaxSensingRadius,
            s.MinSteeringRate, s.MaxSteeringRate, s.SensingRadiusMutationAmplitude, s.SteeringRateMutationAmplitude,
            s.EnableNeuralControl, s.NeuralWeightMin, s.NeuralWeightMax, s.InitialNeuralWeightVariationAmplitude,
            s.SpeedModulationAmplitude, s.NeuralWeightMutationAmplitude, s.InitialPredatorCount, s.InitialPredatorEnergy,
            s.PredationEnergyTransferRatio, radius, efficiency, minRadius, maxRadius, minEfficiency, maxEfficiency,
            initialRadius, initialEfficiency, radiusMutation, efficiencyMutation);

    internal static Dictionary<string, object> Defaults => new()
    {
        ["OrganismAttackRadius"] = 8.0, ["OrganismAttackEfficiency"] = 1.0,
        ["MinAttackRadius"] = 0.0, ["MaxAttackRadius"] = 50.0, ["MinAttackEfficiency"] = 0.0, ["MaxAttackEfficiency"] = 1.0,
        ["InitialAttackRadiusVariationAmplitude"] = 0.0, ["InitialAttackEfficiencyVariationAmplitude"] = 0.0,
        ["AttackRadiusMutationAmplitude"] = 1.0, ["AttackEfficiencyMutationAmplitude"] = 0.05
    };
}

public sealed class AttackTraitSettingsTests
{
    private static SimulationSettings Independent() => new(91, 0.25, 120, 90, 3, 7, 12, 2, 9, 0.75, 55,
        170, 60, 40, 23, 4, 0.2, 1, 190, 0.1, 6, 1.5, 0.04,
        35, 3, 8, 0.3, 2, 110, 0.2, 6.5, 2.5, 0.15, true, -2, 3, 0.6, 0.4, 0.2, 2, 130, 0.375,
        12, 0.6, 2, 40, 0.1, 0.9, 3, 0.2, 2, 0.04);

    [Fact]
    public void FiftyTwoArgumentsMapEveryPropertyToIndependentlyStatedValues()
    {
        Dictionary<string, object> expected = new()
        {
            ["Seed"] = 91UL, ["TickDurationSeconds"] = 0.25, ["WorldWidth"] = 120.0, ["WorldHeight"] = 90.0,
            ["InitialOrganismCount"] = 3, ["InitialFoodCount"] = 7, ["OrganismSpeed"] = 12.0,
            ["OrganismEnergyDrainPerSecond"] = 2.0, ["FoodPickupRadius"] = 9.0,
            ["FoodRegenerationIntervalSeconds"] = 0.75, ["MaxFoodCount"] = 55,
            ["ReproductionEnergyThreshold"] = 170.0, ["ReproductionEnergyCost"] = 60.0, ["OffspringInitialEnergy"] = 40.0,
            ["MaxOrganismCount"] = 23, ["InitialSpeedVariationAmplitude"] = 4.0, ["InitialMetabolismVariationAmplitude"] = 0.2,
            ["MinSpeed"] = 1.0, ["MaxSpeed"] = 190.0, ["MinMetabolism"] = 0.1, ["MaxMetabolism"] = 6.0,
            ["SpeedMutationAmplitude"] = 1.5, ["MetabolismMutationAmplitude"] = 0.04,
            ["OrganismSensingRadius"] = 35.0, ["OrganismSteeringRate"] = 3.0,
            ["InitialSensingRadiusVariationAmplitude"] = 8.0, ["InitialSteeringRateVariationAmplitude"] = 0.3,
            ["MinSensingRadius"] = 2.0, ["MaxSensingRadius"] = 110.0, ["MinSteeringRate"] = 0.2, ["MaxSteeringRate"] = 6.5,
            ["SensingRadiusMutationAmplitude"] = 2.5, ["SteeringRateMutationAmplitude"] = 0.15,
            ["EnableNeuralControl"] = true, ["NeuralWeightMin"] = -2.0, ["NeuralWeightMax"] = 3.0,
            ["InitialNeuralWeightVariationAmplitude"] = 0.6, ["SpeedModulationAmplitude"] = 0.4,
            ["NeuralWeightMutationAmplitude"] = 0.2, ["InitialPredatorCount"] = 2, ["InitialPredatorEnergy"] = 130.0,
            ["PredationEnergyTransferRatio"] = 0.375,
            ["OrganismAttackRadius"] = 12.0, ["OrganismAttackEfficiency"] = 0.6,
            ["MinAttackRadius"] = 2.0, ["MaxAttackRadius"] = 40.0, ["MinAttackEfficiency"] = 0.1, ["MaxAttackEfficiency"] = 0.9,
            ["InitialAttackRadiusVariationAmplitude"] = 3.0, ["InitialAttackEfficiencyVariationAmplitude"] = 0.2,
            ["AttackRadiusMutationAmplitude"] = 2.0, ["AttackEfficiencyMutationAmplitude"] = 0.04
        };
        var actual = Independent(); var properties = typeof(SimulationSettings).GetProperties();
        Assert.Equal(52, properties.Length); Assert.Equal(expected.Keys.Order(), properties.Select(p => p.Name).Order());
        foreach (var property in properties) { Assert.Null(property.SetMethod); Assert.Equal(expected[property.Name], property.GetValue(actual)); }
        var constructor = Assert.Single(typeof(SimulationSettings).GetConstructors(), c => c.GetParameters().Length == 52);
        Assert.Equal(AttackSettings.Defaults.Keys.Select(n => char.ToLowerInvariant(n[0]) + n[1..]), constructor.GetParameters().Skip(42).Select(p => p.Name));
    }

    [Fact]
    public void EveryHistoricalConstructorKeepsEveryOldPropertyAndSuppliesTheTenDefaults()
    {
        var defaults = SimulationSettings.CreateDefault(); var properties = typeof(SimulationSettings).GetProperties();
        var constructors = typeof(SimulationSettings).GetConstructors().Where(c => c.GetParameters().Length <= 42).ToArray();
        Assert.Equal(new[] { 4, 6, 7, 9, 11, 15, 23, 33, 38, 39, 41, 42 }, constructors.Select(c => c.GetParameters().Length).Order());
        foreach (var constructor in constructors)
        {
            var arguments = constructor.GetParameters().Select(p => typeof(SimulationSettings)
                .GetProperty(p.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!.GetValue(defaults)).ToArray();
            var actual = (SimulationSettings)constructor.Invoke(arguments);
            foreach (var p in properties) Assert.Equal(p.GetValue(defaults), p.GetValue(actual));
            foreach (var pair in AttackSettings.Defaults) Assert.Equal(pair.Value, typeof(SimulationSettings).GetProperty(pair.Key)!.GetValue(actual));
        }
    }

    [Fact]
    public void LegacyBaselineExpansionAndCustomSettingsPreserveAllFortyTwoOriginalProperties()
    {
        var expanded = new SimulationSettings(42, 1, 100, 80, 1, 0, double.MaxValue, double.MaxValue, 0);
        Assert.Equal(double.MaxValue, expanded.OrganismSpeed); Assert.Equal(double.MaxValue, expanded.MaxSpeed);
        Assert.Equal(double.MaxValue, expanded.OrganismEnergyDrainPerSecond); Assert.Equal(double.MaxValue, expanded.MaxMetabolism);
        var legacy = PredatorFixture.Settings(neural: true, oldVariation: true, newVariation: true, predators: 5);
        var copied = AttackSettings.Copy(legacy, radius: 12, efficiency: 0.4, initialRadius: 3, initialEfficiency: 0.2);
        foreach (var p in typeof(SimulationSettings).GetProperties().Where(p => !AttackSettings.Defaults.ContainsKey(p.Name)))
            Assert.Equal(p.GetValue(legacy), p.GetValue(copied));
    }

    public static IEnumerable<object[]> InvalidNonnegativeFields()
    {
        foreach (var property in AttackSettings.Defaults.Keys)
            foreach (var value in new[] { -double.Epsilon, -1.0, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
                yield return [char.ToLowerInvariant(property[0]) + property[1..], value];
    }

    [Theory]
    [MemberData(nameof(InvalidNonnegativeFields))]
    public void AllTenSettingsRejectInvalidNumbersWithExactParameterNames(string parameter, double value)
    {
        var constructor = typeof(SimulationSettings).GetConstructors().Single(c => c.GetParameters().Length == 52);
        var valid = Independent(); var parameters = constructor.GetParameters();
        var arguments = parameters.Select(p => typeof(SimulationSettings).GetProperty(p.Name!, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)!.GetValue(valid)).ToArray();
        arguments[Array.FindIndex(parameters, p => p.Name == parameter)] = value;
        var wrapper = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.Equal(parameter, Assert.IsType<ArgumentOutOfRangeException>(wrapper.InnerException).ParamName);
    }

    [Theory]
    [InlineData("organismAttackEfficiency", 1.0000000000000002, "organismAttackEfficiency")]
    [InlineData("minAttackEfficiency", 1.0000000000000002, "minAttackEfficiency")]
    [InlineData("maxAttackEfficiency", 1.0000000000000002, "maxAttackEfficiency")]
    [InlineData("maxAttackRadius", 2.0, "maxAttackRadius")]
    [InlineData("maxAttackRadius", 1.0, "maxAttackRadius")]
    [InlineData("minAttackRadius", 40.0, "maxAttackRadius")]
    [InlineData("minAttackEfficiency", 0.9, "maxAttackEfficiency")]
    [InlineData("maxAttackEfficiency", 0.1, "maxAttackEfficiency")]
    [InlineData("organismAttackRadius", 1.0, "organismAttackRadius")]
    [InlineData("organismAttackRadius", 41.0, "organismAttackRadius")]
    [InlineData("organismAttackEfficiency", 0.0, "organismAttackEfficiency")]
    [InlineData("organismAttackEfficiency", 1.0, "organismAttackEfficiency")]
    public void BoundsAndBaselineCrossChecksNameTheInvalidParameter(string parameter, double value, string expected)
    {
        var constructor = typeof(SimulationSettings).GetConstructors().Single(c => c.GetParameters().Length == 52);
        var parameters = constructor.GetParameters(); var valid = Independent();
        var args = parameters.Select(p => typeof(SimulationSettings).GetProperty(p.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!.GetValue(valid)).ToArray();
        args[Array.FindIndex(parameters, p => p.Name == parameter)] = value;
        var wrapper = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(args));
        Assert.Equal(expected, Assert.IsType<ArgumentOutOfRangeException>(wrapper.InnerException).ParamName);
    }

    [Fact]
    public void InclusiveBaselinesExtremeFiniteAmplitudesAndNegativeZeroAreAcceptedExactly()
    {
        var legacy = SimulationSettings.CreateDefault();
        var maximum = AttackSettings.Copy(legacy, radius: double.MaxValue, maxRadius: double.MaxValue, efficiency: 1,
            initialRadius: double.MaxValue, initialEfficiency: double.MaxValue, radiusMutation: double.MaxValue, efficiencyMutation: double.MaxValue);
        Assert.Equal(double.MaxValue, maximum.OrganismAttackRadius); Assert.Equal(double.MaxValue, maximum.AttackEfficiencyMutationAmplitude);
        var zero = AttackSettings.Copy(legacy, radius: -0.0, efficiency: -0.0);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(zero.OrganismAttackRadius));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(zero.OrganismAttackEfficiency));
    }
}
