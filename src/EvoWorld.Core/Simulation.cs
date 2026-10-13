using EvoWorld.Core.Random;

namespace EvoWorld.Core;

/// <summary>A single-threaded fixed-step simulation. Not thread-safe.</summary>
public sealed class Simulation
{
    private sealed record Advancement(SimulationState State, Pcg32State Random, long NextFoodId, long NextOrganismId);
    internal const double OffspringPositionOffsetRadius = 5.0;
    internal const double OffspringHeadingOffsetMaxRadians = 0.1;
    private Advancement _advancement;
    private readonly long _regenerationTicks;

    public SimulationSettings Settings { get; }
    public SimulationState State => _advancement.State;
    public long TickCount => State.TickCount;
    internal Pcg32State RandomState => _advancement.Random;
    internal long NextFoodId => _advancement.NextFoodId;
    internal long NextOrganismId => _advancement.NextOrganismId;
    // Narrow test seam: force the real WorldState constructor to reject its input
    // after tentative spawn draws, without exposing any public mutation facility.
    internal bool FailWorldConstructionForTesting { get; set; }

    public Simulation(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        _regenerationTicks = FoodRegenerationSchedule.Calculate(settings.FoodRegenerationIntervalSeconds, settings.TickDurationSeconds);
        var random = new Pcg32(settings.Seed);
        var state = new SimulationState(WorldInitializer.Create(settings, random), 0);
        _advancement = new(state, random.CaptureState(), settings.InitialFoodCount, settings.InitialOrganismCount);
    }

    // Test-only starting snapshot seam: immutable input, validated invariants,
    // and the same post-initialization RNG as the public constructor. Counts may
    // shrink or include regenerated food; IDs may have gaps. Explicit history is
    // optional for testing previously allocated IDs no longer present in the world.
    internal Simulation(SimulationSettings settings, WorldState world, long tickCount = 0,
        long? nextFoodId = null, long? nextOrganismId = null)
        : this(settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(tickCount);
        ValidateStartingWorld(settings, world);
        var minimumId = Math.Max((long)settings.InitialFoodCount,
            world.FoodItems.Count == 0 ? 0 : (long)world.FoodItems[^1].Id + 1);
        var nextId = nextFoodId ?? minimumId;
        if (nextId < minimumId || nextId > int.MaxValue + 1L)
            throw new ArgumentOutOfRangeException(nameof(nextFoodId));
        var minimumOrganismId = Math.Max((long)settings.InitialOrganismCount,
            world.Organisms.Count == 0 ? 0 : (long)world.Organisms[^1].Id + 1);
        var nextOrganism = nextOrganismId ?? minimumOrganismId;
        if (nextOrganism < minimumOrganismId || nextOrganism > int.MaxValue + 1L)
            throw new ArgumentOutOfRangeException(nameof(nextOrganismId));
        _advancement = new(new SimulationState(world, tickCount), _advancement.Random, nextId, nextOrganism);
    }

    public void AdvanceTick()
    {
        var previousAdvancement = _advancement;
        var previous = previousAdvancement.State;
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
        var nextRandom = previousAdvancement.Random;
        var nextId = previousAdvancement.NextFoodId;
        var nextOrganismId = previousAdvancement.NextOrganismId;
        Pcg32? random = null;
        if (_regenerationTicks != 0 && nextTick % _regenerationTicks == 0
            && remainingFood.Count < Settings.MaxFoodCount)
        {
            var id = checked((int)nextId);
            random = new Pcg32(nextRandom);
            var x = WorldInitializer.NextScaled(random, Settings.WorldWidth);
            var y = WorldInitializer.NextScaled(random, Settings.WorldHeight);
            if (!ValidPosition(x, y, previous.World))
                throw new OverflowException("Regenerated food must lie within the world.");
            remainingFood.Add(new Food(id, x, y, WorldInitializer.InitialFoodEnergy));
            nextId++;
        }
        var offspring = new List<Organism>();
        var parentCount = survivors.Count;
        for (var index = 0; index < parentCount; index++)
        {
            var parent = survivors[index];
            if (parentCount + offspring.Count >= Settings.MaxOrganismCount
                || parent.Energy < Settings.ReproductionEnergyThreshold) continue;
            var parentEnergy = parent.Energy - Settings.ReproductionEnergyCost;
            if (!double.IsFinite(parentEnergy))
                throw new OverflowException("Parent energy after reproduction must be finite.");
            if (parentEnergy <= 0) continue;
            var id = checked((int)nextOrganismId);
            random ??= new Pcg32(nextRandom);
            var positionUnit = random.NextUInt() / 4294967296.0;
            var offsetAngle = positionUnit * (2 * Math.PI);
            var childX = ClampOffspringPosition(parent.X, OffspringPositionOffsetRadius * Math.Cos(offsetAngle), Settings.WorldWidth);
            var childY = ClampOffspringPosition(parent.Y, OffspringPositionOffsetRadius * Math.Sin(offsetAngle), Settings.WorldHeight);
            var headingUnit = random.NextUInt() / 4294967296.0;
            var headingDelta = (2 * headingUnit - 1) * OffspringHeadingOffsetMaxRadians;
            var childHeading = Normalize(parent.HeadingRadians + headingDelta);
            offspring.Add(new Organism(id, childX, childY, childHeading, Settings.OffspringInitialEnergy));
            survivors[index] = new Organism(parent.Id, parent.X, parent.Y, parent.HeadingRadians, parentEnergy);
            nextOrganismId++;
        }
        survivors.AddRange(offspring); // Newborns first act next tick; IDs exceed all surviving parent IDs.
        nextRandom = random?.CaptureState() ?? nextRandom;
        var world = new WorldState(Settings.WorldWidth, Settings.WorldHeight,
            FailWorldConstructionForTesting ? null! : survivors, remainingFood);
        var next = new SimulationState(world, nextTick);
        var advancement = new Advancement(next, nextRandom, nextId, nextOrganismId);
        _advancement = advancement; // Sole commit: state, stream position, and ID history.
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

    private static double ClampOffspringPosition(double position, double offset, double dimension)
    {
        // Offset is finite and bounded by five. Compare after addition to preserve
        // ordinary rounding; saturation also handles a positive overflowing candidate.
        var candidate = position + offset;
        if (candidate < 0) return 0;
        return candidate >= dimension ? Math.BitDecrement(dimension) : candidate;
    }

    private static void ValidateStartingWorld(SimulationSettings settings, WorldState world)
    {
        if (world.WorldWidth != settings.WorldWidth || world.WorldHeight != settings.WorldHeight
            || world.Organisms.Count > Math.Max(settings.InitialOrganismCount, settings.MaxOrganismCount)
            || world.FoodItems.Count > Math.Max(settings.InitialFoodCount, settings.MaxFoodCount))
            throw new ArgumentException("Starting world must match dimensions and supported population limits.", nameof(world));
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
