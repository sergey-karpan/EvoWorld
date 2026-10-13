using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class SteeringSettingsTests
{
    [Fact]
    public void DefaultsAndLegacyEntityConstructorsHaveIntendedCompatibility()
    {
        var s=SimulationSettings.CreateDefault();
        Assert.Equal((30.0,2.0,0.0,0.0,0.0,100.0,0.0,5.0,2.0,.1),
            (s.OrganismSensingRadius,s.OrganismSteeringRate,s.InitialSensingRadiusVariationAmplitude,s.InitialSteeringRateVariationAmplitude,
             s.MinSensingRadius,s.MaxSensingRadius,s.MinSteeringRate,s.MaxSteeringRate,s.SensingRadiusMutationAmplitude,s.SteeringRateMutationAmplitude));
        Assert.Equal((0.0,0.0),(new Organism(0,0,0,0,100).SensingRadius,new Organism(0,0,0,0,100,1,1).SteeringRate));
        Assert.Null(typeof(Organism).GetProperty("SensingRadius")!.SetMethod); Assert.Null(typeof(Organism).GetProperty("SteeringRate")!.SetMethod);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidSensoryValuesIdentifyAllTenParameters(double value)
    {
        (string Name,Func<SimulationSettings> Make)[] cases=
        [
            ("organismSensingRadius",()=>SteeringFixture.Settings(sensing:value)),
            ("organismSteeringRate",()=>SteeringFixture.Settings(steering:value)),
            ("initialSensingRadiusVariationAmplitude",()=>SteeringFixture.Settings(sensingVariation:value)),
            ("initialSteeringRateVariationAmplitude",()=>SteeringFixture.Settings(steeringVariation:value)),
            ("minSensingRadius",()=>SteeringFixture.Settings(minSensing:value)),
            ("maxSensingRadius",()=>SteeringFixture.Settings(maxSensing:value)),
            ("minSteeringRate",()=>SteeringFixture.Settings(minSteering:value)),
            ("maxSteeringRate",()=>SteeringFixture.Settings(maxSteering:value)),
            ("sensingRadiusMutationAmplitude",()=>SteeringFixture.Settings(sensingMutation:value)),
            ("steeringRateMutationAmplitude",()=>SteeringFixture.Settings(steeringMutation:value))
        ];
        foreach(var c in cases)Assert.Equal(c.Name,Assert.Throws<ArgumentOutOfRangeException>(c.Make).ParamName);
        Assert.Equal("sensingRadius",Assert.Throws<ArgumentOutOfRangeException>(()=>new Organism(0,0,0,0,100,1,1,value,1)).ParamName);
        Assert.Equal("steeringRate",Assert.Throws<ArgumentOutOfRangeException>(()=>new Organism(0,0,0,0,100,1,1,1,value)).ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EqualAndReversedSensoryRangesAreRejected(double minimum)
    {
        Assert.Equal("maxSensingRadius",Assert.Throws<ArgumentOutOfRangeException>(()=>SteeringFixture.Settings(minSensing:minimum,maxSensing:1)).ParamName);
        Assert.Equal("maxSteeringRate",Assert.Throws<ArgumentOutOfRangeException>(()=>SteeringFixture.Settings(minSteering:minimum,maxSteering:1)).ParamName);
    }

    [Fact]
    public void BaselinesMustBeInExplicitBoundsAndZeroIsValid()
    {
        Assert.Equal("organismSensingRadius",Assert.Throws<ArgumentOutOfRangeException>(()=>SteeringFixture.Settings(maxSensing:29)).ParamName);
        Assert.Equal("organismSteeringRate",Assert.Throws<ArgumentOutOfRangeException>(()=>SteeringFixture.Settings(minSteering:3)).ParamName);
        var simulation=new Simulation(SteeringFixture.Settings(sensing:0,steering:0,sensingMutation:0,steeringMutation:0));
        Assert.Equal((0.0,0.0),(simulation.State.World.Organisms[0].SensingRadius,simulation.State.World.Organisms[0].SteeringRate));
    }
}
