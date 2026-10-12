using EvoWorld.Core.Random;

namespace EvoWorld.Core;

public static class WorldInitializer
{
    private const double InitialOrganismEnergy = 100.0;
    private const double InitialFoodEnergy = 20.0;

    /// <summary>
    /// Creates organisms first, consuming X, Y, heading per ascending ID,
    /// then food, consuming X, Y per ascending ID. No placement retries.
    /// </summary>
    public static WorldState Create(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Create(settings, new Pcg32(settings.Seed));
    }

    internal static WorldState Create(SimulationSettings settings, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var organisms = new Organism[settings.InitialOrganismCount];
        var foodItems = new Food[settings.InitialFoodCount];

        for (var id = 0; id < organisms.Length; id++)
        {
            var x = NextScaled(random, settings.WorldWidth);
            var y = NextScaled(random, settings.WorldHeight);
            var heading = NextScaled(random, 2.0 * Math.PI);
            organisms[id] = new Organism(id, x, y, heading, InitialOrganismEnergy);
        }

        for (var id = 0; id < foodItems.Length; id++)
        {
            var x = NextScaled(random, settings.WorldWidth);
            var y = NextScaled(random, settings.WorldHeight);
            foodItems[id] = new Food(id, x, y, InitialFoodEnergy);
        }

        return new WorldState(settings.WorldWidth, settings.WorldHeight, organisms, foodItems);
    }

    private static double NextScaled(Pcg32 random, double upperBound)
    {
        var value = (random.NextUInt() / 4294967296.0) * upperBound;
        // Subnormal dimensions can round a product up to the exclusive bound.
        return value < upperBound ? value : Math.BitDecrement(upperBound);
    }
}
