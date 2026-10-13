using EvoWorld.Core.Random;

namespace EvoWorld.Core;

/// <summary>A single-threaded fixed-step simulation. Not thread-safe.</summary>
public sealed class Simulation
{
    private readonly Pcg32 _random;
    private SimulationState _state;

    public SimulationSettings Settings { get; }
    public SimulationState State => _state;
    public long TickCount => _state.TickCount;
    internal Pcg32State RandomState => _random.CaptureState();

    public Simulation(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        _random = new Pcg32(settings.Seed);
        _state = new SimulationState(WorldInitializer.Create(settings, _random), 0);
    }

    // Test-only starting snapshot seam: immutable input, validated invariants,
    // and the same post-initialization RNG as the public constructor. Counts may
    // shrink and IDs may have gaps after removal; no new entities are allowed.
    internal Simulation(SimulationSettings settings, WorldState world, long tickCount = 0)
        : this(settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(tickCount);
        ValidateStartingWorld(settings, world);
        _state = new SimulationState(world, tickCount);
    }

    public void AdvanceTick()
    {
        var previous = _state;
        var nextTick = checked(previous.TickCount + 1);
        var energyCost = Settings.OrganismEnergyDrainPerSecond * Settings.TickDurationSeconds;
        if (!double.IsFinite(energyCost))
            throw new OverflowException("Energy cost must be finite.");

        var survivors = new List<Organism>(previous.World.Organisms.Count);
        var foodItems = previous.World.FoodItems;
        var consumed = new bool[foodItems.Count];
        var radius = FoodDistance.FromLength(Settings.FoodPickupRadius);
        for (var index = 0; index < previous.World.Organisms.Count; index++)
        {
            var organism = previous.World.Organisms[index];
            var energy = organism.Energy - energyCost;
            if (!double.IsFinite(energy))
                throw new OverflowException("Organism energy after drain must be finite.");
            if (energy <= 0.0) continue; // Death precedes movement and feeding.

            var distance = Settings.OrganismSpeed * Settings.TickDurationSeconds;
            if (!double.IsFinite(distance))
                throw new OverflowException("Movement distance must be finite.");
            var (cosine, sine) = Direction(organism.HeadingRadians);
            var x = BoundaryReflection.Reflect(organism.X, cosine * distance, Settings.WorldWidth);
            var y = BoundaryReflection.Reflect(organism.Y, sine * distance, Settings.WorldHeight);
            var heading = organism.HeadingRadians;
            if (x.Reflected && y.Reflected)
                heading = Normalize(heading + Math.PI);
            else if (x.Reflected)
                heading = Normalize(Math.PI - heading);
            else if (y.Reflected)
                heading = Normalize(-heading);
            var selected = -1;
            var closest = default(FoodDistance);
            for (var foodIndex = 0; foodIndex < foodItems.Count; foodIndex++)
            {
                if (consumed[foodIndex]) continue;
                var food = foodItems[foodIndex];
                var separation = FoodDistance.Between(x.Position, y.Position, food.X, food.Y);
                if (separation.CompareTo(radius) > 0) continue;
                if (selected < 0 || separation.CompareTo(closest) < 0
                    || (separation.CompareTo(closest) == 0 && food.Id < foodItems[selected].Id))
                {
                    selected = foodIndex;
                    closest = separation;
                }
            }
            if (selected >= 0)
            {
                energy += foodItems[selected].Energy;
                if (!double.IsFinite(energy))
                    throw new OverflowException("Organism energy after feeding must be finite.");
                consumed[selected] = true;
            }
            survivors.Add(new Organism(organism.Id, x.Position, y.Position, heading, energy));
        }

        var remainingFood = new List<Food>(foodItems.Count);
        for (var index = 0; index < foodItems.Count; index++)
            if (!consumed[index]) remainingFood.Add(foodItems[index]);
        var world = new WorldState(Settings.WorldWidth, Settings.WorldHeight, survivors, remainingFood);
        var next = new SimulationState(world, nextTick);
        _state = next; // Sole commit; movement and survival never use _random.
    }

    private static (double Cosine, double Sine) Direction(double heading)
    {
        // Exact cardinal directions avoid spurious perpendicular motion on walls.
        if (heading == 0.0) return (1.0, 0.0);
        if (heading == Math.PI / 2.0) return (0.0, 1.0);
        if (heading == Math.PI) return (-1.0, 0.0);
        if (heading == 3.0 * Math.PI / 2.0) return (0.0, -1.0);
        return (Math.Cos(heading), Math.Sin(heading));
    }

    private static double Normalize(double heading)
    {
        var fullTurn = 2.0 * Math.PI;
        var result = heading % fullTurn;
        if (result < 0.0) result += fullTurn;
        return result < fullTurn ? result : Math.BitDecrement(fullTurn);
    }

    private static void ValidateStartingWorld(SimulationSettings settings, WorldState world)
    {
        if (world.WorldWidth != settings.WorldWidth || world.WorldHeight != settings.WorldHeight
            || world.Organisms.Count > settings.InitialOrganismCount || world.FoodItems.Count > settings.InitialFoodCount)
            throw new ArgumentException("Starting world must match dimensions and not exceed initial counts.", nameof(world));
        for (var i = 0; i < world.Organisms.Count; i++)
        {
            var entity = world.Organisms[i];
            if (entity.Id < 0 || (i > 0 && entity.Id <= world.Organisms[i - 1].Id)
                || !ValidPosition(entity.X, entity.Y, world)
                || !double.IsFinite(entity.HeadingRadians) || entity.HeadingRadians < 0.0
                || entity.HeadingRadians >= 2.0 * Math.PI || !double.IsFinite(entity.Energy) || entity.Energy <= 0.0)
                throw new ArgumentException("Starting organisms must satisfy world invariants.", nameof(world));
        }
        for (var i = 0; i < world.FoodItems.Count; i++)
        {
            var entity = world.FoodItems[i];
            if (entity.Id < 0 || (i > 0 && entity.Id <= world.FoodItems[i - 1].Id)
                || !ValidPosition(entity.X, entity.Y, world)
                || !double.IsFinite(entity.Energy) || entity.Energy <= 0.0)
                throw new ArgumentException("Starting food must satisfy world invariants.", nameof(world));
        }
    }

    private static bool ValidPosition(double x, double y, WorldState world) =>
        double.IsFinite(x) && x >= 0.0 && x < world.WorldWidth
        && double.IsFinite(y) && y >= 0.0 && y < world.WorldHeight;
}
