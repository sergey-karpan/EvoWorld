namespace EvoWorld.Core;

/// <summary>An immutable initial world snapshot with independently owned collections.</summary>
public sealed class WorldState
{
    public double WorldWidth { get; }
    public double WorldHeight { get; }
    public IReadOnlyList<Organism> Organisms { get; }
    public IReadOnlyList<Food> FoodItems { get; }

    public WorldState(double worldWidth, double worldHeight,
        IEnumerable<Organism> organisms, IEnumerable<Food> foodItems)
    {
        if (!double.IsFinite(worldWidth) || worldWidth <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(worldWidth));
        if (!double.IsFinite(worldHeight) || worldHeight <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(worldHeight));
        ArgumentNullException.ThrowIfNull(organisms);
        ArgumentNullException.ThrowIfNull(foodItems);

        var organismCopy = organisms.ToArray();
        var foodCopy = foodItems.ToArray();
        if (Array.Exists(organismCopy, organism => organism is null))
            throw new ArgumentException("Organisms must not contain null entries.", nameof(organisms));
        if (Array.Exists(foodCopy, food => food is null))
            throw new ArgumentException("Food items must not contain null entries.", nameof(foodItems));

        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        Organisms = Array.AsReadOnly(organismCopy);
        FoodItems = Array.AsReadOnly(foodCopy);
    }
}
