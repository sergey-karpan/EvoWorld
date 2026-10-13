using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class SensoryInheritanceTests
{
    [Fact]
    public void ZeroMutationInheritsOwnParentSensoryTraitsButStillUsesSixDraws()
    {
        var settings=SteeringFixture.Settings(2,cap:4,sensingMutation:0,steeringMutation:0);
        var simulation=SteeringFixture.Create([SteeringFixture.Organism(sensing:12.25,steering:.125,energy:160),SteeringFixture.Organism(x:50,sensing:40,steering:3,energy:160,id:9)],[],settings);
        var random=new Pcg32(simulation.RandomState); for(var i=0;i<12;i++)random.NextUInt();
        simulation.AdvanceTick();
        Assert.Equal((12.25,.125),(simulation.State.World.Organisms[2].SensingRadius,simulation.State.World.Organisms[2].SteeringRate));
        Assert.Equal((40.0,3.0),(simulation.State.World.Organisms[3].SensingRadius,simulation.State.World.Organisms[3].SteeringRate));
        Assert.Equal(random.CaptureState(),simulation.RandomState);
    }

    [Fact]
    public void SensoryMutationUsesFifthAndSixthBirthOutputsFromIndependentReference()
    {
        var simulation=SteeringFixture.Create([SteeringFixture.Organism(sensing:30,steering:2,energy:160)],[],SteeringFixture.Settings(cap:2));
        simulation.AdvanceTick(); var child=simulation.State.World.Organisms[1];
        Assert.Equal(29.805649541318417,child.SensingRadius);
        Assert.Equal(1.9086902182083576,child.SteeringRate);
        Assert.Equal(new Pcg32State(15569043114566951143UL,3),simulation.RandomState);
        Assert.Equal((30.0,2.0),(simulation.State.World.Organisms[0].SensingRadius,simulation.State.World.Organisms[0].SteeringRate));
        Assert.Equal(50,child.Energy);
    }

    [Fact]
    public void SensoryMutationClampsCustomAdultsWithoutChangingThem()
    {
        var simulation=SteeringFixture.Create([SteeringFixture.Organism(sensing:1000,steering:100,energy:160)],[],SteeringFixture.Settings(cap:2,sensingMutation:0,steeringMutation:0));
        simulation.AdvanceTick();
        Assert.Equal((1000.0,100.0),(simulation.State.World.Organisms[0].SensingRadius,simulation.State.World.Organisms[0].SteeringRate));
        Assert.Equal((100.0,5.0),(simulation.State.World.Organisms[1].SensingRadius,simulation.State.World.Organisms[1].SteeringRate));
    }
}
