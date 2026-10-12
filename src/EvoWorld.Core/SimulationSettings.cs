namespace EvoWorld.Core;

/// <summary>Immutable configuration for one simulation run.</summary>
public sealed class SimulationSettings
{
    private const int DefaultOrganismCount = 20;
    private const int DefaultFoodCount = 100;
    private const double DefaultOrganismSpeed = 50.0;
    public ulong Seed { get; }
    public double TickDurationSeconds { get; }
    public double WorldWidth { get; }
    public double WorldHeight { get; }
    public int InitialOrganismCount { get; }
    public int InitialFoodCount { get; }
    public double OrganismSpeed { get; }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, DefaultOrganismCount, DefaultFoodCount)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight,
            initialOrganismCount, initialFoodCount, DefaultOrganismSpeed)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount, double organismSpeed)
    {
        ValidatePositiveFinite(tickDurationSeconds, nameof(tickDurationSeconds));
        ValidatePositiveFinite(worldWidth, nameof(worldWidth));
        ValidatePositiveFinite(worldHeight, nameof(worldHeight));
        ArgumentOutOfRangeException.ThrowIfNegative(initialOrganismCount);
        ArgumentOutOfRangeException.ThrowIfNegative(initialFoodCount);
        if (!double.IsFinite(organismSpeed) || organismSpeed < 0.0)
            throw new ArgumentOutOfRangeException(nameof(organismSpeed));

        Seed = seed;
        TickDurationSeconds = tickDurationSeconds;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        InitialOrganismCount = initialOrganismCount;
        InitialFoodCount = initialFoodCount;
        OrganismSpeed = organismSpeed;
    }

    public static SimulationSettings CreateDefault() => new(42UL, 1.0 / 30.0, 1000.0, 700.0);

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
    }
}
