namespace EvoWorld.Core;

/// <summary>Immutable configuration for one simulation run.</summary>
public sealed class SimulationSettings
{
    public ulong Seed { get; }
    public double TickDurationSeconds { get; }
    public double WorldWidth { get; }
    public double WorldHeight { get; }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight)
    {
        ValidatePositiveFinite(tickDurationSeconds, nameof(tickDurationSeconds));
        ValidatePositiveFinite(worldWidth, nameof(worldWidth));
        ValidatePositiveFinite(worldHeight, nameof(worldHeight));

        Seed = seed;
        TickDurationSeconds = tickDurationSeconds;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
    }

    public static SimulationSettings CreateDefault() => new(42UL, 1.0 / 30.0, 1000.0, 700.0);

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
    }
}
