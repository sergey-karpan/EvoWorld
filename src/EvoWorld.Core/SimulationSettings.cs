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
    public double OrganismEnergyDrainPerSecond { get; }
    public double FoodPickupRadius { get; }
    public double FoodRegenerationIntervalSeconds { get; }
    public int MaxFoodCount { get; }
    public double ReproductionEnergyThreshold { get; }
    public double ReproductionEnergyCost { get; }
    public double OffspringInitialEnergy { get; }
    public int MaxOrganismCount { get; }
    public double InitialSpeedVariationAmplitude { get; }
    public double InitialMetabolismVariationAmplitude { get; }
    public double MinSpeed { get; }
    public double MaxSpeed { get; }
    public double MinMetabolism { get; }
    public double MaxMetabolism { get; }
    public double SpeedMutationAmplitude { get; }
    public double MetabolismMutationAmplitude { get; }

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
        : this(seed, tickDurationSeconds, worldWidth, worldHeight,
            initialOrganismCount, initialFoodCount, organismSpeed, 1.0, 8.0)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount, double organismSpeed,
        double organismEnergyDrainPerSecond, double foodPickupRadius)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount,
            initialFoodCount, organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius, 2.0, 200)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount, double organismSpeed,
        double organismEnergyDrainPerSecond, double foodPickupRadius,
        double foodRegenerationIntervalSeconds, int maxFoodCount)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount,
            initialFoodCount, organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius,
            foodRegenerationIntervalSeconds, maxFoodCount, 150, 50, 50, 500)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount, double organismSpeed,
        double organismEnergyDrainPerSecond, double foodPickupRadius,
        double foodRegenerationIntervalSeconds, int maxFoodCount,
        double reproductionEnergyThreshold, double reproductionEnergyCost,
        double offspringInitialEnergy, int maxOrganismCount)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount, initialFoodCount,
            organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius, foodRegenerationIntervalSeconds,
            maxFoodCount, reproductionEnergyThreshold, reproductionEnergyCost, offspringInitialEnergy,
            maxOrganismCount, 0, 0, 0, Math.Max(200, organismSpeed), 0,
            Math.Max(5, organismEnergyDrainPerSecond), 1, 0.05)
    {
    }

    public SimulationSettings(ulong seed, double tickDurationSeconds, double worldWidth, double worldHeight,
        int initialOrganismCount, int initialFoodCount, double organismSpeed,
        double organismEnergyDrainPerSecond, double foodPickupRadius,
        double foodRegenerationIntervalSeconds, int maxFoodCount,
        double reproductionEnergyThreshold, double reproductionEnergyCost,
        double offspringInitialEnergy, int maxOrganismCount,
        double initialSpeedVariationAmplitude, double initialMetabolismVariationAmplitude,
        double minSpeed, double maxSpeed, double minMetabolism, double maxMetabolism,
        double speedMutationAmplitude, double metabolismMutationAmplitude)
    {
        ValidatePositiveFinite(tickDurationSeconds, nameof(tickDurationSeconds));
        ValidatePositiveFinite(worldWidth, nameof(worldWidth));
        ValidatePositiveFinite(worldHeight, nameof(worldHeight));
        ArgumentOutOfRangeException.ThrowIfNegative(initialOrganismCount);
        ArgumentOutOfRangeException.ThrowIfNegative(initialFoodCount);
        if (!double.IsFinite(organismSpeed) || organismSpeed < 0.0)
            throw new ArgumentOutOfRangeException(nameof(organismSpeed));
        if (!double.IsFinite(organismEnergyDrainPerSecond) || organismEnergyDrainPerSecond < 0.0)
            throw new ArgumentOutOfRangeException(nameof(organismEnergyDrainPerSecond));
        if (!double.IsFinite(foodPickupRadius) || foodPickupRadius < 0.0)
            throw new ArgumentOutOfRangeException(nameof(foodPickupRadius));
        ValidatePositiveFinite(foodRegenerationIntervalSeconds, nameof(foodRegenerationIntervalSeconds));
        ArgumentOutOfRangeException.ThrowIfNegative(maxFoodCount);
        ValidatePositiveFinite(reproductionEnergyThreshold, nameof(reproductionEnergyThreshold));
        ValidatePositiveFinite(reproductionEnergyCost, nameof(reproductionEnergyCost));
        ValidatePositiveFinite(offspringInitialEnergy, nameof(offspringInitialEnergy));
        if (offspringInitialEnergy > reproductionEnergyCost)
            throw new ArgumentOutOfRangeException(nameof(offspringInitialEnergy));
        ArgumentOutOfRangeException.ThrowIfNegative(maxOrganismCount);
        ValidateNonnegativeFinite(initialSpeedVariationAmplitude, nameof(initialSpeedVariationAmplitude));
        ValidateNonnegativeFinite(initialMetabolismVariationAmplitude, nameof(initialMetabolismVariationAmplitude));
        ValidateNonnegativeFinite(minSpeed, nameof(minSpeed));
        ValidateNonnegativeFinite(maxSpeed, nameof(maxSpeed));
        ValidateNonnegativeFinite(minMetabolism, nameof(minMetabolism));
        ValidateNonnegativeFinite(maxMetabolism, nameof(maxMetabolism));
        if (minSpeed >= maxSpeed) throw new ArgumentOutOfRangeException(nameof(maxSpeed));
        if (minMetabolism >= maxMetabolism) throw new ArgumentOutOfRangeException(nameof(maxMetabolism));
        if (organismSpeed < minSpeed || organismSpeed > maxSpeed) throw new ArgumentOutOfRangeException(nameof(organismSpeed));
        if (organismEnergyDrainPerSecond < minMetabolism || organismEnergyDrainPerSecond > maxMetabolism)
            throw new ArgumentOutOfRangeException(nameof(organismEnergyDrainPerSecond));
        ValidateNonnegativeFinite(speedMutationAmplitude, nameof(speedMutationAmplitude));
        ValidateNonnegativeFinite(metabolismMutationAmplitude, nameof(metabolismMutationAmplitude));

        Seed = seed;
        TickDurationSeconds = tickDurationSeconds;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        InitialOrganismCount = initialOrganismCount;
        InitialFoodCount = initialFoodCount;
        OrganismSpeed = organismSpeed;
        OrganismEnergyDrainPerSecond = organismEnergyDrainPerSecond;
        FoodPickupRadius = foodPickupRadius;
        FoodRegenerationIntervalSeconds = foodRegenerationIntervalSeconds;
        MaxFoodCount = maxFoodCount;
        ReproductionEnergyThreshold = reproductionEnergyThreshold;
        ReproductionEnergyCost = reproductionEnergyCost;
        OffspringInitialEnergy = offspringInitialEnergy;
        MaxOrganismCount = maxOrganismCount;
        InitialSpeedVariationAmplitude = initialSpeedVariationAmplitude;
        InitialMetabolismVariationAmplitude = initialMetabolismVariationAmplitude;
        MinSpeed = minSpeed; MaxSpeed = maxSpeed;
        MinMetabolism = minMetabolism; MaxMetabolism = maxMetabolism;
        SpeedMutationAmplitude = speedMutationAmplitude; MetabolismMutationAmplitude = metabolismMutationAmplitude;
    }

    public static SimulationSettings CreateDefault() => new(42UL, 1.0 / 30.0, 1000.0, 700.0);

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
    }

    private static void ValidateNonnegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName);
    }
}
