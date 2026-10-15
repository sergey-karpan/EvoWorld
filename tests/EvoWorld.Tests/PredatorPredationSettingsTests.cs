using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorPredationSettingsTests
{
    [Theory]
    [InlineData(-double.Epsilon)]
    [InlineData(-1.0)]
    [InlineData(1.0000000000000002)]
    [InlineData(double.MaxValue)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidRatioIsRejectedEvenWithoutNeuralControl(double ratio) =>
        Assert.Equal("predationEnergyTransferRatio", Assert.Throws<ArgumentOutOfRangeException>(() => PredationFixture.Settings(ratio: ratio)).ParamName);

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.Epsilon)]
    [InlineData(0.8)]
    [InlineData(0.9999999999999999)]
    [InlineData(1.0)]
    public void ClosedIntervalIsAcceptedWithoutChangingValue(double ratio) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(ratio), BitConverter.DoubleToInt64Bits(PredationFixture.Settings(ratio: ratio).PredationEnergyTransferRatio));

    [Fact]
    public void NegativeZeroRatioRetainsItsBinaryRepresentation()
    {
        var negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(PredationFixture.Settings(ratio: negativeZero).PredationEnergyTransferRatio));
    }

    [Fact]
    public void AllElevenHistoricalSignaturesRemainAndDefaultRatioIsPointEight()
    {
        var defaults = SimulationSettings.CreateDefault();
        var constructors = typeof(SimulationSettings).GetConstructors().Where(c => c.GetParameters().Length <= 42).ToArray();
        Assert.Equal(new[] { 4, 6, 7, 9, 11, 15, 23, 33, 38, 39, 41, 42 }, constructors.Select(c => c.GetParameters().Length).Order());
        Assert.Equal(0.8, defaults.PredationEnergyTransferRatio);
        foreach (var constructor in constructors.Where(c => c.GetParameters().Length <= 41))
        {
            var arguments = constructor.GetParameters().Select(p => typeof(SimulationSettings)
                .GetProperty(char.ToUpperInvariant(p.Name![0]) + p.Name[1..])!.GetValue(defaults)).ToArray();
            var actual = (SimulationSettings)constructor.Invoke(arguments);
            Assert.Equal(0.8, actual.PredationEnergyTransferRatio);
            foreach (var property in typeof(SimulationSettings).GetProperties()) Assert.Equal(property.GetValue(defaults), property.GetValue(actual));
        }
        Assert.Null(typeof(SimulationSettings).GetProperty(nameof(SimulationSettings.PredationEnergyTransferRatio))!.SetMethod);
    }

    [Fact]
    public void FortyTwoArgumentConstructorMapsEveryFieldIndependently()
    {
        Dictionary<string, object> expected = new()
        {
            ["OrganismAttackRadius"] = 8.0, ["OrganismAttackEfficiency"] = 1.0,
            ["MinAttackRadius"] = 0.0, ["MaxAttackRadius"] = 50.0,
            ["MinAttackEfficiency"] = 0.0, ["MaxAttackEfficiency"] = 1.0,
            ["InitialAttackRadiusVariationAmplitude"] = 0.0, ["InitialAttackEfficiencyVariationAmplitude"] = 0.0,
            ["AttackRadiusMutationAmplitude"] = 1.0, ["AttackEfficiencyMutationAmplitude"] = 0.05,
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
            ["PredationEnergyTransferRatio"] = 0.375
        };
        var actual = new SimulationSettings(91, 0.25, 120, 90, 3, 7, 12, 2, 9, 0.75, 55,
            170, 60, 40, 23, 4, 0.2, 1, 190, 0.1, 6, 1.5, 0.04,
            35, 3, 8, 0.3, 2, 110, 0.2, 6.5, 2.5, 0.15, true, -2, 3, 0.6, 0.4, 0.2, 2, 130, 0.375);
        Assert.Equal(expected.Keys.Order(), typeof(SimulationSettings).GetProperties().Select(p => p.Name).Order());
        foreach (var property in typeof(SimulationSettings).GetProperties()) Assert.Equal(expected[property.Name], property.GetValue(actual));
    }
}
