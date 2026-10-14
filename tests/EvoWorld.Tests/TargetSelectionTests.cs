using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class TargetSelectionTests
{
    [Fact]
    public void TargetEnumHasExactlyThreeStableValues()
    {
        Assert.Equal(new[] { TargetKind.None, TargetKind.Food, TargetKind.Prey }, Enum.GetValues<TargetKind>());
        Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<TargetKind>().Select(k => (int)k));
    }

    [Fact]
    public void HerbivoreUsesClosestFoodThenSmallestFoodIdAndSkipsConsumedFood()
    {
        var herbivore = PursuitFixture.Organism(Species.Herbivore);
        Food[] food = [new(43, 55, 40, 20), new(7, 45, 40, 20), new(99, 51, 40, 20)];
        var consumed = new bool[3];
        var nearest = TargetSensing.Resolve(herbivore, food, consumed, [PursuitFixture.Organism(id: 100, x: 50)]);
        Assert.Equal(new SensedTarget(TargetKind.Food, 51, 40), nearest);
        consumed[2] = true;
        Assert.Equal(new SensedTarget(TargetKind.Food, 45, 40), TargetSensing.Resolve(herbivore, food, consumed, []));
        consumed[1] = true;
        Assert.Equal(new SensedTarget(TargetKind.Food, 55, 40), TargetSensing.Resolve(herbivore, food, consumed, []));
        consumed[0] = true; Assert.Equal(default, TargetSensing.Resolve(herbivore, food, consumed, []));
    }

    [Fact]
    public void PredatorIgnoresFoodSelfAndPredatorsAndUsesSmallestPreyIdForTies()
    {
        var predator = PursuitFixture.Organism(id: 17);
        Organism[] previous = [predator, PursuitFixture.Organism(id: 1),
            PursuitFixture.Organism(Species.Herbivore, id: int.MaxValue, x: 55),
            PursuitFixture.Organism(Species.Herbivore, id: 43, x: 45)];
        Assert.Equal(new SensedTarget(TargetKind.Prey, 45, 40),
            TargetSensing.Resolve(predator, [new Food(0, 50, 40, 20)], [false], previous));
        Assert.Equal(default, TargetSensing.Resolve(predator, [], [], previous.Take(2).ToArray()));
    }

    [Theory]
    [InlineData(0.0, 0.0, TargetKind.None)]
    [InlineData(1.0, 0.0, TargetKind.Prey)]
    [InlineData(30.0, 30.0, TargetKind.Prey)]
    [InlineData(29.999999999999996, 30.0, TargetKind.None)]
    public void RadiusIsInclusiveAndZeroDisablesCoincidence(double radius, double offset, TargetKind expected)
    {
        var predator = PursuitFixture.Organism(sensing: radius);
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 50 + offset);
        Assert.Equal(expected, TargetSensing.Resolve(predator, [], [], [predator, prey]).Kind);
    }

    [Fact]
    public void NearestPreyWinsBeforeIdAndSelectionDoesNotMutateItsSnapshot()
    {
        var predator = PursuitFixture.Organism();
        var near = PursuitFixture.Organism(Species.Herbivore, id: int.MaxValue, x: 51);
        var far = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 55);
        var simulation = PursuitFixture.Create([predator, far, near]);
        var state = simulation.State; var random = simulation.RandomState;
        Assert.Equal(new SensedTarget(TargetKind.Prey, 51, 40), TargetSensing.Resolve(predator, [], [], state.World.Organisms));
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
        Assert.Same(near, state.World.Organisms[2]); Assert.Equal(0, simulation.TickCount);
    }

    [Fact]
    public void HugeDistancesAreRankedWithoutSquaredDistanceOverflow()
    {
        var predator = PursuitFixture.Organism(x: 0, y: 0, sensing: double.MaxValue);
        var outside = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 1.3e308, y: 1.3e308);
        var inside = PursuitFixture.Organism(Species.Herbivore, id: int.MaxValue, x: 1e308, y: 1e308);
        Assert.Equal(TargetKind.None, TargetSensing.Resolve(predator, [], [], [outside]).Kind);
        Assert.Equal(new SensedTarget(TargetKind.Prey, 1e308, 1e308), TargetSensing.Resolve(predator, [], [], [outside, inside]));
        var a = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 1e308, y: 0);
        var b = PursuitFixture.Organism(Species.Herbivore, id: 7, x: 0, y: 1e308);
        Assert.Equal(new SensedTarget(TargetKind.Prey, 0, 1e308), TargetSensing.Resolve(predator, [], [], [a, b]));
    }

    [Fact]
    public void SubnormalDistancesPreserveRadiusBoundariesWithoutRoundingIntoCoincidence()
    {
        var predator = PursuitFixture.Organism(x: 0, y: 0, sensing: double.Epsilon);
        var edge = PursuitFixture.Organism(Species.Herbivore, id: 43, x: double.Epsilon, y: 0);
        Assert.Equal(TargetKind.Prey, TargetSensing.Resolve(predator, [], [], [edge]).Kind);
        var diagonal = PursuitFixture.Organism(Species.Herbivore, id: 43, x: double.Epsilon, y: double.Epsilon);
        Assert.Equal(TargetKind.None, TargetSensing.Resolve(predator, [], [], [diagonal]).Kind);
        var outside = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 2 * double.Epsilon, y: 0);
        Assert.Equal(TargetKind.None, TargetSensing.Resolve(predator, [], [], [outside]).Kind);
    }

    [Fact]
    public void SelectionDoesNotPrecomputePreyMetabolicDrainOrFutureDeaths()
    {
        var predator = PursuitFixture.Organism();
        var prey = PursuitFixture.Organism(Species.Herbivore, id: 43, x: 51, metabolism: double.MaxValue, energy: double.Epsilon);
        Assert.Equal(TargetKind.Prey, TargetSensing.Resolve(predator, [], [], [prey]).Kind);
    }
}
