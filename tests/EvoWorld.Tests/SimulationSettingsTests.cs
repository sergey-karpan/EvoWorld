using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SimulationSettingsTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(ulong.MaxValue)]
    public void ValidConstructionPreservesEveryValueIncludingZeroSeed(ulong seed)
    {
        var settings = new SimulationSettings(seed, 0.125, 123.5, 456.75);

        Assert.Equal(seed, settings.Seed);
        Assert.Equal(0.125, settings.TickDurationSeconds);
        Assert.Equal(123.5, settings.WorldWidth);
        Assert.Equal(456.75, settings.WorldHeight);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidTickDurationIdentifiesParameter(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationSettings(42UL, value, 1000.0, 700.0));

        Assert.Equal("tickDurationSeconds", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidWorldWidthIdentifiesParameter(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationSettings(42UL, 1.0 / 30.0, value, 700.0));

        Assert.Equal("worldWidth", exception.ParamName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidWorldHeightIdentifiesParameter(double value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationSettings(42UL, 1.0 / 30.0, 1000.0, value));

        Assert.Equal("worldHeight", exception.ParamName);
    }

    [Fact]
    public void DefaultConfigurationIsValidAndContainsApprovedValues()
    {
        var settings = SimulationSettings.CreateDefault();

        Assert.Equal(42UL, settings.Seed);
        Assert.Equal(1.0 / 30.0, settings.TickDurationSeconds);
        Assert.Equal(1000.0, settings.WorldWidth);
        Assert.Equal(700.0, settings.WorldHeight);
    }

    [Fact]
    public void IndependentInstancesWithSameInputsExposeIdenticalValues()
    {
        var first = new SimulationSettings(12345678901234567890UL, 0.25, 12.5, 34.5);
        var second = new SimulationSettings(12345678901234567890UL, 0.25, 12.5, 34.5);

        Assert.NotSame(first, second);
        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.TickDurationSeconds, second.TickDurationSeconds);
        Assert.Equal(first.WorldWidth, second.WorldWidth);
        Assert.Equal(first.WorldHeight, second.WorldHeight);
    }

    [Fact]
    public void PositiveFiniteExtremesAreAcceptedWithoutClamping()
    {
        var settings = new SimulationSettings(0UL, double.Epsilon, double.MaxValue, double.Epsilon);

        Assert.Equal(double.Epsilon, settings.TickDurationSeconds);
        Assert.Equal(double.MaxValue, settings.WorldWidth);
        Assert.Equal(double.Epsilon, settings.WorldHeight);
    }
}
