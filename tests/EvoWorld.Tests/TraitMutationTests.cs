using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitMutationTests
{
    [Theory]
    [InlineData(0U, 9)]
    [InlineData(2147483648U, 10)]
    [InlineData(3221225472U, 10.5)]
    public void UniformSignedMappingHasExpectedAmplitude(uint output, double expected)
    {
        Assert.Equal(expected, TraitVariation.Apply(10, 1, 0, 200, output));
    }

    [Fact]
    public void ClampHandlesBoundsOverflowAndExactZeroAmplitude()
    {
        Assert.Equal(0, TraitVariation.Apply(1, 10, 0, 200, 0));
        Assert.Equal(200, TraitVariation.Apply(double.MaxValue, double.MaxValue, 0, 200, uint.MaxValue));
        Assert.Equal(0, TraitVariation.Apply(0, double.MaxValue, 0, 200, 0));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(TraitVariation.Apply(-0.0, 0, 0, 200, 1)));
        Assert.Equal(double.Epsilon, TraitVariation.Apply(double.Epsilon, 0, 0, 200, 1));
    }

    [Fact]
    public void OutsideRangeAdultsArePreservedAndTheirChildrenClampEvenAtZeroAmplitude()
    {
        var simulation = TraitFixture.Create([new Organism(0, 50, 40, 0, 200, 300, 10)],
            settings: TraitFixture.Settings(speedAmplitude: 0, metabolismAmplitude: 0, duration: 0.001));
        simulation.AdvanceTick();
        Assert.Equal((300.0, 10.0), (simulation.State.World.Organisms[0].Speed, simulation.State.World.Organisms[0].Metabolism));
        Assert.Equal((200.0, 5.0), (simulation.State.World.Organisms[1].Speed, simulation.State.World.Organisms[1].Metabolism));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void EntityRejectsInvalidTraitsWithCorrectParameter(double value)
    {
        Assert.Equal("speed", Assert.Throws<ArgumentOutOfRangeException>(() => new Organism(0, 0, 0, 0, 100, value, 1)).ParamName);
        Assert.Equal("metabolism", Assert.Throws<ArgumentOutOfRangeException>(() => new Organism(0, 0, 0, 0, 100, 1, value)).ParamName);
    }

    [Fact]
    public void LegacyEntityAndExplicitExtremesAreImmutableAndStructurallyValid()
    {
        var legacy = new Organism(0, 0, 0, 0, 100);
        Assert.Equal((50.0, 1.0), (legacy.Speed, legacy.Metabolism));
        var explicitValues = new Organism(0, 0, 0, 0, 100, double.MaxValue, 0);
        Assert.Equal(double.MaxValue, explicitValues.Speed); Assert.Equal(0, explicitValues.Metabolism);
        Assert.Null(typeof(Organism).GetProperty("Speed")!.SetMethod); Assert.Null(typeof(Organism).GetProperty("Metabolism")!.SetMethod);
    }
}
