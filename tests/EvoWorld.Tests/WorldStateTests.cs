using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class WorldStateTests
{
    [Fact]
    public void SnapshotDefensivelyCopiesSourceCollectionsAndPreservesOrder()
    {
        var initial = WorldInitializer.Create(new SimulationSettings(42UL, 0.125, 100, 80, 2, 2));
        var organisms = initial.Organisms.ToList();
        var food = initial.FoodItems.ToArray();
        var snapshot = new WorldState(100, 80, organisms, food);

        organisms.Reverse();
        organisms.Clear();
        food[0] = initial.FoodItems[1];

        Assert.Equal(2, snapshot.Organisms.Count);
        Assert.Same(initial.Organisms[0], snapshot.Organisms[0]);
        Assert.Same(initial.Organisms[1], snapshot.Organisms[1]);
        Assert.Same(initial.FoodItems[0], snapshot.FoodItems[0]);
        Assert.Same(initial.FoodItems[1], snapshot.FoodItems[1]);
    }

    [Fact]
    public void CollectionCastsCannotModifyInitialState()
    {
        var world = WorldInitializer.Create(new SimulationSettings(42UL, 0.125, 100, 80, 2, 2));
        var organisms = Assert.IsAssignableFrom<IList<Organism>>(world.Organisms);
        var food = Assert.IsAssignableFrom<IList<Food>>(world.FoodItems);

        Assert.True(organisms.IsReadOnly);
        Assert.True(food.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => organisms.Add(world.Organisms[0]));
        Assert.Throws<NotSupportedException>(() => organisms.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => organisms[0] = world.Organisms[1]);
        Assert.Throws<NotSupportedException>(() => organisms.Clear());
        Assert.Throws<NotSupportedException>(() => food.Add(world.FoodItems[0]));
        Assert.Throws<NotSupportedException>(() => food.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => food[0] = world.FoodItems[1]);
        Assert.Throws<NotSupportedException>(() => food.Clear());
        Assert.Equal(new[] { 0, 1 }, world.Organisms.Select(o => o.Id));
        Assert.Equal(new[] { 0, 1 }, world.FoodItems.Select(f => f.Id));
        Assert.Same(world.Organisms, world.Organisms);
        Assert.Same(world.FoodItems, world.FoodItems);
    }

    [Fact]
    public void NullCollectionsAndEntriesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new WorldState(100, 80, null!, []));
        Assert.Throws<ArgumentNullException>(() => new WorldState(100, 80, [], null!));
        Assert.Throws<ArgumentException>(() => new WorldState(100, 80, new Organism[] { null! }, []));
        Assert.Throws<ArgumentException>(() => new WorldState(100, 80, [], new Food[] { null! }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidSnapshotDimensionsAreRejected(double value)
    {
        Assert.Equal("worldWidth", Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldState(value, 80, [], [])).ParamName);
        Assert.Equal("worldHeight", Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldState(100, value, [], [])).ParamName);
    }
}
