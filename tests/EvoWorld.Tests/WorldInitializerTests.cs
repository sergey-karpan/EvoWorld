using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public class WorldInitializerTests
{
    [Fact]
    public void Seed42FixtureMatchesIndependentlyCalculatedEntityBits()
    {
        // Task 2 official-C golden outputs, in consumption order:
        // 4df1ccf9 e5838752 58ed9e10 f3e37b51 e7664374 6afde4a8
        // 8712391e 738fc318 0b1f9c57 41b0f580.
        // Independently calculated in Python: output / 2**32 * scale;
        // scales = [100, 80, 2*pi, 100, 80, 2*pi, 100, 80, 100, 80].
        // IEEE-754 bits obtained with struct.pack/unpack, not this initializer.
        var world = WorldInitializer.Create(new SimulationSettings(42UL, 1.0 / 30.0, 100, 80, 2, 2));

        AssertBits(0x403e727411440000UL, world.Organisms[0].X);
        AssertBits(0x4051ee4692680000UL, world.Organisms[0].Y);
        AssertBits(0x40017604be5f60abUL, world.Organisms[0].HeadingRadians);
        AssertBits(0x4057d1370ae90000UL, world.Organisms[1].X);
        AssertBits(0x405213fd45100000UL, world.Organisms[1].Y);
        AssertBits(0x400501fe32114c11UL, world.Organisms[1].HeadingRadians);
        AssertBits(0x404a618f27dc0000UL, world.FoodItems[0].X);
        AssertBits(0x40420e767bc00000UL, world.FoodItems[0].Y);
        AssertBits(0x4011616447f00000UL, world.FoodItems[1].X);
        AssertBits(0x4034874cb8000000UL, world.FoodItems[1].Y);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(ulong.MaxValue)]
    public void InitializationPreservesAllWorldInvariants(ulong seed)
    {
        var settings = new SimulationSettings(seed, 0.125, 100, 80, 17, 23);
        var world = WorldInitializer.Create(settings);

        Assert.Equal(settings.WorldWidth, world.WorldWidth);
        Assert.Equal(settings.WorldHeight, world.WorldHeight);
        Assert.Equal(17, world.Organisms.Count);
        Assert.Equal(23, world.FoodItems.Count);
        Assert.Equal(Enumerable.Range(0, 17), world.Organisms.Select(o => o.Id));
        Assert.Equal(Enumerable.Range(0, 23), world.FoodItems.Select(f => f.Id));
        foreach (var organism in world.Organisms)
        {
            AssertPosition(world, organism.X, organism.Y);
            Assert.True(double.IsFinite(organism.HeadingRadians));
            Assert.True(organism.HeadingRadians >= 0 && organism.HeadingRadians < 2.0 * Math.PI);
            Assert.Equal(100.0, organism.Energy);
        }
        foreach (var food in world.FoodItems)
        {
            AssertPosition(world, food.X, food.Y);
            Assert.Equal(20.0, food.Energy);
        }
    }

    [Fact]
    public void EquivalentSettingsAndRepeatedCallsProduceIdenticalIndependentWorlds()
    {
        var settings = new SimulationSettings(0UL, 0.125, 100, 80, 5, 7);
        var first = WorldInitializer.Create(settings);
        var expectedOrganisms = OrganismValues(first);
        var expectedFood = FoodValues(first);

        for (var run = 0; run < 3; run++)
        {
            WorldInitializer.Create(new SimulationSettings(999UL, 0.25, 250, 300, 3, 4));
            var repeated = WorldInitializer.Create(settings);
            var independent = WorldInitializer.Create(new SimulationSettings(0UL, 0.125, 100, 80, 5, 7));
            Assert.NotSame(first, repeated);
            Assert.NotSame(first.Organisms[0], repeated.Organisms[0]);
            Assert.Equal(expectedOrganisms, OrganismValues(repeated));
            Assert.Equal(expectedFood, FoodValues(repeated));
            Assert.Equal(expectedOrganisms, OrganismValues(independent));
            Assert.Equal(expectedFood, FoodValues(independent));
        }
    }

    [Fact]
    public void RepresentativeSeedsProduceDifferentPlacements()
    {
        var zero = WorldInitializer.Create(new SimulationSettings(0UL, 0.125, 100, 80, 2, 2));
        var one = WorldInitializer.Create(new SimulationSettings(1UL, 0.125, 100, 80, 2, 2));

        Assert.False(OrganismValues(zero).SequenceEqual(OrganismValues(one)));
        Assert.False(FoodValues(zero).SequenceEqual(FoodValues(one)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(4, 5)]
    public void CountsAndExactRandomConsumptionOrderMatchVerifiedPcg32(int organisms, int food)
    {
        var world = WorldInitializer.Create(new SimulationSettings(42UL, 0.125, 100, 80, organisms, food));
        var random = new Pcg32(42UL);

        Assert.Equal(organisms, world.Organisms.Count);
        Assert.Equal(food, world.FoodItems.Count);
        foreach (var organism in world.Organisms)
        {
            Assert.Equal(random.NextUInt() / 4294967296.0 * 100, organism.X);
            Assert.Equal(random.NextUInt() / 4294967296.0 * 80, organism.Y);
            Assert.Equal(random.NextUInt() / 4294967296.0 * (2.0 * Math.PI), organism.HeadingRadians);
        }
        foreach (var item in world.FoodItems)
        {
            Assert.Equal(random.NextUInt() / 4294967296.0 * 100, item.X);
            Assert.Equal(random.NextUInt() / 4294967296.0 * 80, item.Y);
        }
    }

    [Fact]
    public void NullSettingsAreRejectedExplicitly()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => WorldInitializer.Create(null!));
        Assert.Equal("settings", exception.ParamName);
    }

    [Theory]
    [InlineData(double.Epsilon, double.Epsilon)]
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(double.Epsilon, double.MaxValue)]
    [InlineData(1e-308, 2e-308)]
    public void ExtremeFiniteDimensionsRetainExclusiveBounds(double width, double height)
    {
        var world = WorldInitializer.Create(new SimulationSettings(42UL, double.Epsilon, width, height, 8, 11));
        foreach (var organism in world.Organisms)
            AssertPosition(world, organism.X, organism.Y);
        foreach (var food in world.FoodItems)
            AssertPosition(world, food.X, food.Y);
    }

    [Fact]
    public void SmallestPositiveDimensionsCorrectRoundedUpperBoundToZero()
    {
        // Seed 42's first Y unit exceeds 0.5: scaling by epsilon rounds up to
        // epsilon before correction. Zero is the only valid coordinate here.
        var world = WorldInitializer.Create(new SimulationSettings(42UL, 0.125, double.Epsilon, double.Epsilon, 2, 2));
        Assert.All(world.Organisms, o => { Assert.Equal(0.0, o.X); Assert.Equal(0.0, o.Y); });
        Assert.All(world.FoodItems, f => { Assert.Equal(0.0, f.X); Assert.Equal(0.0, f.Y); });
    }

    private static void AssertBits(ulong expected, double actual) =>
        Assert.Equal(expected, BitConverter.DoubleToUInt64Bits(actual));

    private static void AssertPosition(WorldState world, double x, double y)
    {
        Assert.True(double.IsFinite(x) && x >= 0 && x < world.WorldWidth);
        Assert.True(double.IsFinite(y) && y >= 0 && y < world.WorldHeight);
    }

    private static (int, double, double, double, double)[] OrganismValues(WorldState world) =>
        world.Organisms.Select(o => (o.Id, o.X, o.Y, o.HeadingRadians, o.Energy)).ToArray();

    private static (int, double, double, double)[] FoodValues(WorldState world) =>
        world.FoodItems.Select(f => (f.Id, f.X, f.Y, f.Energy)).ToArray();
}
