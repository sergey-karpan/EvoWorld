using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralSettingsTests
{
    [Fact]
    public void EveryLegacySettingsSignaturePreservesFiveNewDefaults()
    {
        var defaults = SimulationSettings.CreateDefault();
        var constructors = typeof(SimulationSettings).GetConstructors().Where(c => c.GetParameters().Length < 38).ToArray();
        Assert.Equal(new[] { 4, 6, 7, 9, 11, 15, 23, 33 }, constructors.Select(c => c.GetParameters().Length).Order());
        foreach (var constructor in constructors)
        {
            var args = constructor.GetParameters().Select(p => typeof(SimulationSettings)
                .GetProperty(char.ToUpperInvariant(p.Name![0]) + p.Name[1..])!.GetValue(defaults)).ToArray();
            var settings = (SimulationSettings)constructor.Invoke(args);
            Assert.False(settings.EnableNeuralControl); Assert.Equal(-1, settings.NeuralWeightMin); Assert.Equal(1, settings.NeuralWeightMax);
            Assert.Equal(0.5, settings.InitialNeuralWeightVariationAmplitude); Assert.Equal(0.5, settings.SpeedModulationAmplitude);
        }
        Assert.Equal(38, typeof(SimulationSettings).GetConstructors().Single(c => c.GetParameters().Length == 38).GetParameters().Length);
    }

    public static IEnumerable<object[]> InvalidSettings()
    {
        foreach (var enabled in new[] { false, true })
        {
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, Math.BitDecrement(-8.0), 0.1 })
                yield return [enabled, "neuralWeightMin", value];
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, Math.BitIncrement(8.0), -0.1 })
                yield return [enabled, "neuralWeightMax", value];
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -double.Epsilon })
                yield return [enabled, "initialNeuralWeightVariationAmplitude", value];
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -double.Epsilon, 1.0, Math.BitIncrement(1.0) })
                yield return [enabled, "speedModulationAmplitude", value];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void NeuralSettingsValidateEvenWhenControlIsDisabled(bool enabled, string parameter, double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => parameter switch
        {
            "neuralWeightMin" => NeuralFixture.Settings(enabled, weightMin: value),
            "neuralWeightMax" => NeuralFixture.Settings(enabled, weightMax: value),
            "initialNeuralWeightVariationAmplitude" => NeuralFixture.Settings(enabled, amplitude: value),
            _ => NeuralFixture.Settings(enabled, modulation: value)
        });
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualBoundsAreRejectedAndZeroCenteredEndpointRangesAccepted(bool enabled)
    {
        Assert.Equal("neuralWeightMax", Assert.Throws<ArgumentOutOfRangeException>(() => NeuralFixture.Settings(enabled, weightMin: 0, weightMax: 0)).ParamName);
        Assert.Equal(0, NeuralFixture.Settings(enabled, weightMin: 0).NeuralWeightMin);
        Assert.Equal(0, NeuralFixture.Settings(enabled, weightMax: 0).NeuralWeightMax);
    }

    [Fact]
    public void ExtremePermittedValuesAndZeroAmplitudesAreImmutable()
    {
        var settings = NeuralFixture.Settings(weightMin: -8, weightMax: 8, amplitude: double.MaxValue, modulation: Math.BitDecrement(1.0));
        Assert.True(settings.EnableNeuralControl); Assert.Equal(double.MaxValue, settings.InitialNeuralWeightVariationAmplitude);
        Assert.Equal(Math.BitDecrement(1.0), settings.SpeedModulationAmplitude);
        settings = NeuralFixture.Settings(amplitude: 0, modulation: 0);
        Assert.Equal(0, settings.InitialNeuralWeightVariationAmplitude); Assert.Equal(0, settings.SpeedModulationAmplitude);
        foreach (var name in new[] { "EnableNeuralControl", "NeuralWeightMin", "NeuralWeightMax", "InitialNeuralWeightVariationAmplitude", "SpeedModulationAmplitude" })
            Assert.Null(typeof(SimulationSettings).GetProperty(name)!.SetMethod);
    }
}
