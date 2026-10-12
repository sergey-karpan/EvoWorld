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
    // and the same post-initialization RNG as the public constructor.
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
        var distance = Settings.OrganismSpeed * Settings.TickDurationSeconds;
        if (!double.IsFinite(distance))
            throw new OverflowException("Movement distance must be finite.");

        var moved = new Organism[previous.World.Organisms.Count];
        for (var index = 0; index < moved.Length; index++)
        {
            var organism = previous.World.Organisms[index];
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
            moved[index] = new Organism(organism.Id, x.Position, y.Position, heading, organism.Energy);
        }

        var world = new WorldState(Settings.WorldWidth, Settings.WorldHeight, moved, previous.World.FoodItems);
        var next = new SimulationState(world, nextTick);
        _state = next; // Commit only after all fallible work. Movement never uses _random.
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
            || world.Organisms.Count != settings.InitialOrganismCount || world.FoodItems.Count != settings.InitialFoodCount)
            throw new ArgumentException("Starting world must match settings dimensions and counts.", nameof(world));
        for (var i = 0; i < world.Organisms.Count; i++)
        {
            var entity = world.Organisms[i];
            if (entity.Id != i || !ValidPosition(entity.X, entity.Y, world)
                || !double.IsFinite(entity.HeadingRadians) || entity.HeadingRadians < 0.0
                || entity.HeadingRadians >= 2.0 * Math.PI || !double.IsFinite(entity.Energy) || entity.Energy <= 0.0)
                throw new ArgumentException("Starting organisms must satisfy world invariants.", nameof(world));
        }
        for (var i = 0; i < world.FoodItems.Count; i++)
        {
            var entity = world.FoodItems[i];
            if (entity.Id != i || !ValidPosition(entity.X, entity.Y, world)
                || !double.IsFinite(entity.Energy) || entity.Energy <= 0.0)
                throw new ArgumentException("Starting food must satisfy world invariants.", nameof(world));
        }
    }

    private static bool ValidPosition(double x, double y, WorldState world) =>
        double.IsFinite(x) && x >= 0.0 && x < world.WorldWidth
        && double.IsFinite(y) && y >= 0.0 && y < world.WorldHeight;
}
