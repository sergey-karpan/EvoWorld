using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class TraitGenerationTests
{
    [Fact]
    public void HomogeneousInitializationPreservesOriginalStreamAndLayout()
    {
        var settings = TraitFixture.Settings(2, 2, speed: 12, metabolism: 0);
        var random = new Pcg32(42); var expected = new Pcg32(42);
        var world = WorldInitializer.Create(settings, random);
        for (var i = 0; i < 10; i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(), random.CaptureState());
        Assert.All(world.Organisms, o => Assert.Equal((12.0, 0.0), (o.Speed, o.Metabolism)));
        Assert.Equal(30.447083548642695, world.Organisms[0].X);
        Assert.Equal(52.76218126527965, world.FoodItems[0].X);
    }

    [Theory]
    [InlineData(10, 0.5)]
    [InlineData(10, 0)]
    [InlineData(0, 0.5)]
    public void VariationComesAfterOriginalPlacementAndUsesTwoDrawsPerOrganism(double speedAmplitude, double metabolismAmplitude)
    {
        var plain = WorldInitializer.Create(TraitFixture.Settings(2, 2));
        var settings = TraitFixture.Settings(2, 2, initialSpeedAmplitude: speedAmplitude, initialMetabolismAmplitude: metabolismAmplitude);
        var random = new Pcg32(42); var expected = new Pcg32(42);
        var varied = WorldInitializer.Create(settings, random);
        for (var i = 0; i < 14; i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(), random.CaptureState());
        Assert.Equal(plain.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)), varied.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)));
        Assert.Equal(plain.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)), varied.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
        Assert.Equal(varied.Organisms.Select(o => (o.Speed, o.Metabolism)), WorldInitializer.Create(settings).Organisms.Select(o => (o.Speed, o.Metabolism)));
        if (speedAmplitude > 0) Assert.NotEqual(varied.Organisms[0].Speed, varied.Organisms[1].Speed);
        if (metabolismAmplitude > 0) Assert.NotEqual(varied.Organisms[0].Metabolism, varied.Organisms[1].Metabolism);
        Assert.All(varied.Organisms, o => { Assert.InRange(o.Speed, 0, 200); Assert.InRange(o.Metabolism, 0, 5); });
    }

    [Fact]
    public void IndependentInitialTraitFixtureMatchesPythonReference()
    {
        var random = new Pcg32(42);
        var world = WorldInitializer.Create(TraitFixture.Settings(1, 1, initialSpeedAmplitude: 10, initialMetabolismAmplitude: 0.5), random);
        Assert.Equal(48.35873205214739, world.Organisms[0].Speed);
        Assert.Equal(1.0276218126527965, world.Organisms[0].Metabolism);
        Assert.Equal(new Pcg32State(13574378311416847701UL, 3), random.CaptureState());
    }
}
