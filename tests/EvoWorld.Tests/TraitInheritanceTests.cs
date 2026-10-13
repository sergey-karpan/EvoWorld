using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class TraitInheritanceTests
{
    [Fact]
    public void ZeroMutationInheritsEachParentsExactTraitsAndStillConsumesFourDraws()
    {
        var settings = TraitFixture.Settings(2, speedAmplitude: 0, metabolismAmplitude: 0);
        var simulation = TraitFixture.Create([new Organism(2, 10, 10, 0, 160, 12.25, 0.125), new Organism(9, 50, 40, 0, 160, 3.5, 2)], settings: settings);
        var random = new Pcg32(simulation.RandomState); for (var i = 0; i < 8; i++) random.NextUInt();
        simulation.AdvanceTick();
        Assert.Equal((12.25, 0.125), (simulation.State.World.Organisms[2].Speed, simulation.State.World.Organisms[2].Metabolism));
        Assert.Equal((3.5, 2.0), (simulation.State.World.Organisms[3].Speed, simulation.State.World.Organisms[3].Metabolism));
        Assert.Equal(random.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void IndependentTwoParentFixtureHasDistinctMovementDrainFeedingAndMutation()
    {
        var simulation = TraitFixture.Create([new Organism(0, 50, 40, 0, 140, 2, 1), new Organism(9, 70, 40, 0, 160, 4, 2)], [new Food(0, 52, 40, 20)]);
        simulation.AdvanceTick(); var world = simulation.State.World;
        Assert.Equal(new[] { 0, 9, 10, 11 }, world.Organisms.Select(o => o.Id).ToArray());
        Assert.Equal(new[] { 109.0, 108.0, 50.0, 50.0 }, world.Organisms.Select(o => o.Energy).ToArray());
        Assert.Equal(52, world.Organisms[0].X); Assert.Equal(74, world.Organisms[1].X); Assert.Empty(world.FoodItems);
        var first = world.Organisms[2]; var second = world.Organisms[3];
        Assert.Equal(2.4911167193204165, first.Speed); Assert.Equal(0.9855133165139704, first.Metabolism);
        Assert.Equal(3.3705166475847363, second.Speed); Assert.Equal(2.024624599888921, second.Metabolism);
        Assert.Equal(56.81481665574779, first.X, 12); Assert.Equal(41.34816192333625, first.Y, 12);
        Assert.Equal(78.37719989071232, second.X, 12); Assert.Equal(42.41663425382245, second.Y, 12);
        Assert.Equal(new Pcg32State(11742290374445839356UL, 3), simulation.RandomState);
        simulation.AdvanceTick(); world = simulation.State.World;
        Assert.Equal(59.30298246133557, world.Organisms[2].X, 12); Assert.Equal(41.22694545093479, world.Organisms[2].Y, 12);
        Assert.Equal(49.01448668348603, world.Organisms[2].Energy, 12);
        Assert.Equal(81.74384950816146, world.Organisms[3].X, 12); Assert.Equal(47.975375400111076, world.Organisms[3].Energy, 12);
        Assert.Equal((2.0, 1.0), (world.Organisms[0].Speed, world.Organisms[0].Metabolism));
    }

    [Fact]
    public void ControlledNonzeroMutationHistoriesRemainReproducibleForFiveThousandTicks()
    {
        Simulation Create() => TraitFixture.Create([new Organism(0, 50, 40, 0, 10000, 0, 0)], settings: TraitFixture.Settings(cap: 30));
        var first = Create(); var second = Create();
        for (var tick = 1; tick <= 5000; tick++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            Assert.Equal(first.TickCount, second.TickCount);
            Assert.Equal(first.RandomState, second.RandomState);
            Assert.Equal(first.NextFoodId, second.NextFoodId); Assert.Equal(first.NextOrganismId, second.NextOrganismId);
            Assert.Equal(first.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism)),
                second.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism)));
            Assert.Equal(first.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)), second.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)));
            Assert.InRange(first.State.World.Organisms.Count, 1, 30);
            if (tick == 1) Assert.True(first.State.World.Organisms[1].Metabolism > 0);
        }
        Assert.True(first.NextOrganismId > 1);
    }
}
