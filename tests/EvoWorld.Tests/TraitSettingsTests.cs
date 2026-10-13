using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitSettingsTests
{
    [Fact]
    public void DefaultsAndLegacyExtremesRemainCompatible()
    {
        var defaults = SimulationSettings.CreateDefault();
        Assert.Equal((0.0, 0.0, 0.0, 200.0, 0.0, 5.0, 1.0, 0.05),
            (defaults.InitialSpeedVariationAmplitude, defaults.InitialMetabolismVariationAmplitude, defaults.MinSpeed, defaults.MaxSpeed,
             defaults.MinMetabolism, defaults.MaxMetabolism, defaults.SpeedMutationAmplitude, defaults.MetabolismMutationAmplitude));
        var legacy = new SimulationSettings(42, 1, 100, 80, 0, 0, double.MaxValue, double.MaxValue, 0);
        Assert.Equal(double.MaxValue, legacy.MaxSpeed); Assert.Equal(double.MaxValue, legacy.MaxMetabolism);
        Assert.Equal(0, new SimulationSettings(42, 1, 100, 80, 0, 0, 0, 0, 0).OrganismSpeed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidNewSettingsIdentifyEachParameter(double value)
    {
        (string Name, Func<SimulationSettings> Construct)[] cases =
        [
            ("initialSpeedVariationAmplitude", () => TraitFixture.Settings(initialSpeedAmplitude: value)),
            ("initialMetabolismVariationAmplitude", () => TraitFixture.Settings(initialMetabolismAmplitude: value)),
            ("minSpeed", () => TraitFixture.Settings(minSpeed: value)),
            ("maxSpeed", () => TraitFixture.Settings(maxSpeed: value)),
            ("minMetabolism", () => TraitFixture.Settings(minMetabolism: value)),
            ("maxMetabolism", () => TraitFixture.Settings(maxMetabolism: value)),
            ("speedMutationAmplitude", () => TraitFixture.Settings(speedAmplitude: value)),
            ("metabolismMutationAmplitude", () => TraitFixture.Settings(metabolismAmplitude: value))
        ];
        foreach (var testCase in cases)
            Assert.Equal(testCase.Name, Assert.Throws<ArgumentOutOfRangeException>(testCase.Construct).ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EqualAndReversedBoundsAreRejected(double minimum)
    {
        Assert.Equal("maxSpeed", Assert.Throws<ArgumentOutOfRangeException>(() => TraitFixture.Settings(minSpeed: minimum, maxSpeed: 1)).ParamName);
        Assert.Equal("maxMetabolism", Assert.Throws<ArgumentOutOfRangeException>(() => TraitFixture.Settings(minMetabolism: minimum, maxMetabolism: 1)).ParamName);
    }

    [Fact]
    public void ExplicitBaselineMustLieWithinBounds()
    {
        Assert.Equal("organismSpeed", Assert.Throws<ArgumentOutOfRangeException>(() => TraitFixture.Settings(maxSpeed: 49)).ParamName);
        Assert.Equal("organismEnergyDrainPerSecond", Assert.Throws<ArgumentOutOfRangeException>(() => TraitFixture.Settings(minMetabolism: 2)).ParamName);
    }

    [Fact]
    public void ZeroAmplitudesAndBaselinesAreValidWithPositiveWidthRanges()
    {
        var settings = TraitFixture.Settings(speed: 0, metabolism: 0, speedAmplitude: 0, metabolismAmplitude: 0);
        var organism = WorldInitializer.Create(settings).Organisms[0];
        Assert.Equal((0.0, 0.0), (organism.Speed, organism.Metabolism));
    }
}
