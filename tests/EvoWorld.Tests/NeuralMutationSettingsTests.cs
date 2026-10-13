using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralMutationSettingsTests
{
    [Fact]
    public void EveryExistingConstructorIncludingThirtyEightPreservesZeroMutation()
    {
        var defaults = SimulationSettings.CreateDefault();
        var constructors = typeof(SimulationSettings).GetConstructors().Where(c => c.GetParameters().Length < 39).ToArray();
        Assert.Equal(new[] { 4, 6, 7, 9, 11, 15, 23, 33, 38 }, constructors.Select(c => c.GetParameters().Length).Order());
        foreach (var constructor in constructors)
        {
            var values = constructor.GetParameters().Select(p => typeof(SimulationSettings)
                .GetProperty(char.ToUpperInvariant(p.Name![0]) + p.Name[1..])!.GetValue(defaults)).ToArray();
            Assert.Equal(0, ((SimulationSettings)constructor.Invoke(values)).NeuralWeightMutationAmplitude);
        }
        Assert.Equal(0, defaults.NeuralWeightMutationAmplitude);
        Assert.Null(typeof(SimulationSettings).GetProperty(nameof(SimulationSettings.NeuralWeightMutationAmplitude))!.SetMethod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.Epsilon)]
    [InlineData(0.125)]
    [InlineData(double.MaxValue)]
    public void ThirtyNineArgumentConstructorAcceptsEveryFiniteNonnegativeAmplitude(double amplitude)
    {
        Assert.Single(typeof(SimulationSettings).GetConstructors(), c => c.GetParameters().Length == 39);
        foreach (var enabled in new[] { true, false })
            Assert.Equal(amplitude, NeuralMutationFixture.Settings(amplitude, enabled).NeuralWeightMutationAmplitude);
    }

    [Theory]
    [InlineData(-double.Epsilon)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidAmplitudeIsRejectedInBothControlModes(double amplitude)
    {
        foreach (var enabled in new[] { true, false })
            Assert.Equal("neuralWeightMutationAmplitude", Assert.Throws<ArgumentOutOfRangeException>(
                () => NeuralMutationFixture.Settings(amplitude, enabled)).ParamName);
    }

    [Fact]
    public void ThirtyNineArgumentConstructorRetainsExistingValidation()
    {
        Assert.Equal("neuralWeightMin", Assert.Throws<ArgumentOutOfRangeException>(() => NeuralMutationFixture.Settings(lower: -9)).ParamName);
        Assert.Equal("neuralWeightMax", Assert.Throws<ArgumentOutOfRangeException>(() => NeuralMutationFixture.Settings(upper: 9)).ParamName);
        Assert.Equal("tickDurationSeconds", Assert.Throws<ArgumentOutOfRangeException>(() => NeuralMutationFixture.Settings(duration: 0)).ParamName);
        Assert.Equal("initialNeuralWeightVariationAmplitude", Assert.Throws<ArgumentOutOfRangeException>(() => NeuralMutationFixture.Settings(initialAmplitude: -1)).ParamName);
    }
}
