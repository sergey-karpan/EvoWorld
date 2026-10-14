using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class SpeciesTests
{
    [Fact]
    public void EnumHasExactlyTwoStableValuesAndOrganismPropertyIsReadOnly()
    {
        Assert.Equal(new[] { Species.Herbivore, Species.Predator }, Enum.GetValues<Species>());
        Assert.Equal(0, (int)Species.Herbivore); Assert.Equal(1, (int)Species.Predator);
        Assert.Null(typeof(Organism).GetProperty(nameof(Organism.Species))!.SetMethod);
    }

    [Fact]
    public void AllLegacyOrganismSignaturesDefaultToHerbivoreAndKeepBrains()
    {
        var brain = NeuralFixture.Brain(0.2);
        Organism[] organisms = [new(0, 1, 1, 0, 100), new(0, 1, 1, 0, 100, 10, 1),
            new(0, 1, 1, 0, 100, 10, 1, 30, 2), new(0, 1, 1, 0, 100, 10, 1, 30, 2, brain)];
        Assert.All(organisms, o => Assert.Equal(Species.Herbivore, o.Species));
        Assert.Same(brain, organisms[^1].Brain);
        Assert.Same(organisms[0].Brain, organisms[2].Brain);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void UndefinedSpeciesIsRejected(int species) =>
        Assert.Equal("species", Assert.Throws<ArgumentOutOfRangeException>(() => PredatorFixture.Organism((Species)species)).ParamName);

    [Theory]
    [InlineData(Species.Herbivore, false)]
    [InlineData(Species.Predator, false)]
    [InlineData(Species.Herbivore, true)]
    [InlineData(Species.Predator, true)]
    public void MovementDrainFeedingAndReflectionPreserveSpeciesAndExactBrain(Species species, bool neural)
    {
        var settings = PredatorFixture.Settings(food: 1, neural: neural, duration: 1, cap: 0, foodCap: 0, pickup: 100);
        var organism = PredatorFixture.Organism(species, x: 99, speed: 10, metabolism: 1, heading: 0);
        var simulation = new Simulation(settings, new WorldState(100, 80, [organism], [new Food(0, 95, 40, 20)]));
        simulation.AdvanceTick();
        var current = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(species, current.Species); Assert.Same(organism.Brain, current.Brain);
        Assert.NotSame(organism, current); Assert.Equal(179, current.Energy);
        Assert.NotEqual(organism.X, current.X); Assert.InRange(current.HeadingRadians, Math.PI / 2, 3 * Math.PI / 2);
        Assert.Empty(simulation.State.World.FoodItems); Assert.Equal(160, organism.Energy);
    }
}
