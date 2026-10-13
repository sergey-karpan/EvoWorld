using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitDrainTests
{
    [Fact]
    public void MetabolismControlsDrainDeathAndZeroDrain()
    {
        var simulation = TraitFixture.Create([new Organism(0, 10, 10, 0, 2, 0, 0), new Organism(2, 20, 20, 0, 2, 0, 1), new Organism(9, 30, 30, 0, 2, 0, 2)]);
        simulation.AdvanceTick();
        Assert.Equal(new[] { 0, 2 }, simulation.State.World.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(new[] { 2.0, 1.0 }, simulation.State.World.Organisms.Select(o => o.Energy).ToArray());
        simulation.AdvanceTick(); Assert.Equal(0, Assert.Single(simulation.State.World.Organisms).Id);
    }

    [Fact]
    public void IndividualMetabolicProductOverflowRollsBackAfterEarlierOrganism()
    {
        SurvivalFixture.AssertRollback(TraitFixture.Create([new Organism(0, 10, 10, 0, 100, 0, 1), new Organism(9, 20, 20, 0, 100, 0, double.MaxValue)], settings: TraitFixture.Settings(2, duration: 2)));
    }

    [Fact]
    public void EmptyWorldDoesNotCalculateUnusedGlobalMetabolicCost()
    {
        var settings = new SimulationSettings(42, 2, 100, 80, 0, 0, 0, double.MaxValue, 0, 1, 0);
        var simulation = new Simulation(settings); simulation.AdvanceTick(); Assert.Equal(1, simulation.TickCount);
    }
}
