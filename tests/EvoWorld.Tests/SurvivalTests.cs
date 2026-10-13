using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class SurvivalTests
{
    [Fact]
    public void ManuallySpecifiedSmallWorldMatchesTwentyTickSurvivalTimeline()
    {
        // Unit speed, unit drain, zero radius. ID 0 eats at x=2 on tick 1;
        // ID 2 eats at x=12 on tick 2. They die before moving on ticks 5 and 6.
        var simulation = SurvivalFixture.Create([new Organism(0, 1, 1, 0, 3), new Organism(2, 10, 1, 0, 5)],
            [new Food(1, 2, 1, 2), new Food(4, 12, 1, 1), new Food(9, 30, 1, 20)], drain: 1, speed: 1);
        var initial = simulation.State;
        var random = simulation.RandomState;
        (int Id, double X, double Energy)[][] timeline =
        [
            [(0, 2, 4), (2, 11, 4)],
            [(0, 3, 3), (2, 12, 4)],
            [(0, 4, 2), (2, 13, 3)],
            [(0, 5, 1), (2, 14, 2)],
            [(2, 15, 1)],
            []
        ];
        for (var tick = 1; tick <= 20; tick++)
        {
            simulation.AdvanceTick();
            var expected = tick <= 6 ? timeline[tick - 1] : [];
            Assert.Equal(expected, simulation.State.World.Organisms.Select(o => (o.Id, o.X, o.Energy)).ToArray());
            Assert.All(simulation.State.World.Organisms, organism =>
            {
                Assert.Equal(1, organism.Y); Assert.Equal(0, organism.HeadingRadians);
                Assert.True(organism.Energy > 0);
            });
            Assert.Equal(tick == 1 ? new[] { 4, 9 } : new[] { 9 }, simulation.State.World.FoodItems.Select(f => f.Id).ToArray());
            Assert.Equal(tick, simulation.TickCount);
            Assert.Equal(random, simulation.RandomState);
            Assert.Same(initial.World.FoodItems[2], simulation.State.World.FoodItems.Last());
        }
        Assert.Equal(3, initial.World.Organisms[0].Energy);
        Assert.Equal(3, initial.World.FoodItems.Count);
        Assert.Equal(0, initial.TickCount);
    }

    [Fact]
    public void IndependentDefaultWorldsRemainExactlyReproducibleForOneThousandTicks()
    {
        var first = new Simulation(SimulationSettings.CreateDefault());
        var second = new Simulation(SimulationSettings.CreateDefault());
        // Defaults include regeneration. Track the established initialization
        // output count and two draws for each newly allocated food ID.
        var expectedRandom = new Pcg32(42);
        for (var i = 0; i < 260; i++) expectedRandom.NextUInt();
        var nextFoodId = first.NextFoodId;
        var nextOrganismId = first.NextOrganismId;
        for (var tick = 1; tick <= 1000; tick++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            Assert.Equal(tick, first.TickCount);
            Assert.Equal(first.TickCount, second.TickCount);
            Assert.Equal(first.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)).ToArray(),
                second.State.World.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate)).ToArray());
            Assert.Equal(first.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray(),
                second.State.World.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray());
            if (first.NextFoodId != nextFoodId)
            {
                Assert.Equal(nextFoodId + 1, first.NextFoodId);
                Assert.Equal(0, tick % 60);
                expectedRandom.NextUInt(); expectedRandom.NextUInt();
                nextFoodId = first.NextFoodId;
            }
            while (nextOrganismId < first.NextOrganismId)
            {
                expectedRandom.NextUInt(); expectedRandom.NextUInt(); expectedRandom.NextUInt(); expectedRandom.NextUInt(); expectedRandom.NextUInt(); expectedRandom.NextUInt(); nextOrganismId++;
            }
            Assert.Equal(nextOrganismId, second.NextOrganismId);
            Assert.Equal(expectedRandom.CaptureState(), first.RandomState);
            Assert.Equal(first.RandomState, second.RandomState);
        }
        Assert.True(first.State.World.FoodItems.Count < 100);
        Assert.All(first.State.World.Organisms, o => Assert.True(o.Energy > 0 && double.IsFinite(o.Energy)));
    }
}
