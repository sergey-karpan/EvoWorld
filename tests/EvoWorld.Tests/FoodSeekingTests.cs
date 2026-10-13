using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class FoodSeekingTests
{
    [Fact]
    public void IndependentEightTickFixtureResolvesTieCompetitionLostTargetsAndReflections()
    {
        var simulation = SteeringFixture.Create(
            [SteeringFixture.Organism(x: 4, y: 4, steering: Math.PI / 2, speed: 1),
             SteeringFixture.Organism(x: 4, y: 4, heading: Math.PI / 2, steering: Math.PI / 4, speed: 1, id: 9)],
            [new Food(2, 4, 5, 20), new Food(9, 5, 4, 20)],
            SteeringFixture.Settings(2, 2, width: 10, height: 10));
        var random = simulation.RandomState; var original = simulation.State;
        // Manually: ID 0 chooses equal-distance food ID 2, consumes it on tick 1.
        // ID 9 sees only food ID 9 and turns pi/4. On tick 3 ID 0 consumes that
        // food; ID 9 preserves heading thereafter. Both hit a wall on tick 7.
        (double X, double Y, double H)[][] expected =
        [
            [(4,5,Math.PI/2),(4.707106781186548,4.707106781186548,Math.PI/4)],
            [(5,5,0),(5.707106781186548,4.707106781186548,0)],
            [(5,4,3*Math.PI/2),(6.707106781186548,4.707106781186548,0)],
            [(5,3,3*Math.PI/2),(7.707106781186548,4.707106781186548,0)],
            [(5,2,3*Math.PI/2),(8.707106781186548,4.707106781186548,0)],
            [(5,1,3*Math.PI/2),(9.707106781186548,4.707106781186548,0)],
            [(5,0,Math.PI/2),(9.292893218813452,4.707106781186548,Math.PI)],
            [(5,1,Math.PI/2),(8.292893218813452,4.707106781186548,Math.PI)]
        ];
        for (var tick = 1; tick <= 8; tick++)
        {
            simulation.AdvanceTick();
            for (var index = 0; index < 2; index++)
            {
                var o = simulation.State.World.Organisms[index]; var e = expected[tick - 1][index];
                Assert.Equal(e.X, o.X, 12); Assert.Equal(e.Y, o.Y, 12); Assert.Equal(e.H, o.HeadingRadians, 14);
                Assert.Equal((1.0,0.0,30.0), (o.Speed,o.Metabolism,o.SensingRadius));
            }
            Assert.Equal(tick < 3 ? new[] {9} : [], simulation.State.World.FoodItems.Select(f => f.Id).ToArray());
            Assert.Equal(random, simulation.RandomState);
            Assert.Equal(tick < 3 ? 120 : 140, simulation.State.World.Organisms[0].Energy);
            Assert.Equal(100, simulation.State.World.Organisms[1].Energy);
        }
        Assert.Equal(2, original.World.FoodItems.Count); Assert.Equal(0, original.TickCount);
    }

    [Fact]
    public void RegeneratedFoodIsInvisibleUntilFollowingTick()
    {
        var simulation = SteeringFixture.Create([SteeringFixture.Organism(heading: 0.7, sensing: 1000, steering: 10)], [], SteeringFixture.Settings(foodCap: 3));
        simulation.AdvanceTick(); Assert.Equal(0.7, simulation.State.World.Organisms[0].HeadingRadians);
        Assert.Single(simulation.State.World.FoodItems);
        simulation.AdvanceTick(); Assert.NotEqual(0.7, simulation.State.World.Organisms[0].HeadingRadians);
    }

    [Fact]
    public void ActiveSteeringReproductionAndRegenerationRemainReproducibleForFiveThousandTicks()
    {
        Simulation Create() => SteeringFixture.Create([SteeringFixture.Organism(speed: 1, energy: 10000)],
            [new Food(0, 10, 20, 20)], SteeringFixture.Settings(food: 1, cap: 20, foodCap: 30));
        var first = Create(); var second = Create(); var initialHeading = first.State.World.Organisms[0].HeadingRadians;
        for (var tick = 1; tick <= 5000; tick++)
        {
            first.AdvanceTick(); second.AdvanceTick();
            Assert.Equal(first.TickCount, second.TickCount); Assert.Equal(first.RandomState, second.RandomState);
            Assert.Equal(first.NextFoodId, second.NextFoodId); Assert.Equal(first.NextOrganismId, second.NextOrganismId);
            Assert.Equal(first.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.Speed,o.Metabolism,o.SensingRadius,o.SteeringRate)),
                second.State.World.Organisms.Select(o => (o.Id,o.X,o.Y,o.HeadingRadians,o.Energy,o.Speed,o.Metabolism,o.SensingRadius,o.SteeringRate)));
            Assert.Equal(first.State.World.FoodItems.Select(f => (f.Id,f.X,f.Y,f.Energy)), second.State.World.FoodItems.Select(f => (f.Id,f.X,f.Y,f.Energy)));
            Assert.InRange(first.State.World.Organisms.Count, 1, 20); Assert.InRange(first.State.World.FoodItems.Count, 0, 30);
            if (tick == 1) { Assert.NotEqual(initialHeading, first.State.World.Organisms[0].HeadingRadians); Assert.Equal(2, first.State.World.Organisms.Count); }
        }
        Assert.True(first.NextOrganismId > 1);
    }
}
