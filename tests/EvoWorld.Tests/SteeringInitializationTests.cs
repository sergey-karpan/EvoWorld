using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class SteeringInitializationTests
{
    [Theory]
    [InlineData(false,false,10)]
    [InlineData(true,false,14)]
    [InlineData(false,true,14)]
    [InlineData(true,true,18)]
    public void FourModesPreserveLayoutAndIndependentPhaseDrawCounts(bool oldVariation,bool newVariation,int draws)
    {
        var plain = WorldInitializer.Create(SteeringFixture.Settings(2,2));
        var settings = SteeringFixture.Settings(2,2,oldSpeedVariation:oldVariation?10:0,oldMetabolismVariation:oldVariation?.5:0,
            sensingVariation:newVariation?10:0,steeringVariation:newVariation?.5:0);
        var random = new Pcg32(42); var expected = new Pcg32(42);
        var world = WorldInitializer.Create(settings,random);
        for(var i=0;i<draws;i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(),random.CaptureState());
        Assert.Equal(plain.Organisms.Select(o=>(o.Id,o.X,o.Y,o.HeadingRadians)),world.Organisms.Select(o=>(o.Id,o.X,o.Y,o.HeadingRadians)));
        Assert.Equal(plain.FoodItems.Select(f=>(f.Id,f.X,f.Y,f.Energy)),world.FoodItems.Select(f=>(f.Id,f.X,f.Y,f.Energy)));
        if(!newVariation) Assert.All(world.Organisms,o=>Assert.Equal((30.0,2.0),(o.SensingRadius,o.SteeringRate)));
        if(!oldVariation) Assert.All(world.Organisms,o=>Assert.Equal((0.0,0.0),(o.Speed,o.Metabolism)));
    }

    [Fact]
    public void CombinedModeMatchesIndependentPythonReferenceAfterPlacementAndOldPhase()
    {
        var random=new Pcg32(42);
        var world=WorldInitializer.Create(SteeringFixture.Settings(1,1,oldSpeedVariation:10,oldMetabolismVariation:.5,sensingVariation:10,steeringVariation:.5),random);
        var o=world.Organisms[0];
        Assert.Equal(0,o.Speed); Assert.Equal(0.027621812652796507,o.Metabolism);
        Assert.Equal(29.028247706592083,o.SensingRadius);
        Assert.Equal(1.5434510910417885,o.SteeringRate);
        Assert.Equal(new Pcg32State(15569043114566951143UL,3),random.CaptureState());
    }

    [Theory]
    [InlineData(10,0)]
    [InlineData(0,.5)]
    public void OneNonzeroNewAmplitudeStillConsumesBothDraws(double sensingAmplitude,double steeringAmplitude)
    {
        var random=new Pcg32(42); var expected=new Pcg32(42);
        WorldInitializer.Create(SteeringFixture.Settings(2,2,sensingVariation:sensingAmplitude,steeringVariation:steeringAmplitude),random);
        for(var i=0;i<14;i++)expected.NextUInt();
        Assert.Equal(expected.CaptureState(),random.CaptureState());
    }
}
