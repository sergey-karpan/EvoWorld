namespace EvoWorld.Core;

/// <summary>Immutable configuration for one simulation run.</summary>
public sealed class SimulationSettings
{
    public double OrganismAttackRadius { get; }
    public double OrganismAttackEfficiency { get; }
    public double MinAttackRadius { get; }
    public double MaxAttackRadius { get; }
    public double MinAttackEfficiency { get; }
    public double MaxAttackEfficiency { get; }
    public double InitialAttackRadiusVariationAmplitude { get; }
    public double InitialAttackEfficiencyVariationAmplitude { get; }
    public double AttackRadiusMutationAmplitude { get; }
    public double AttackEfficiencyMutationAmplitude { get; }
    private const int DefaultOrganismCount = 20;
    private const int DefaultFoodCount = 100;
    private const double DefaultOrganismSpeed = 50.0;
    public int InitialPredatorCount { get; }
    public double InitialPredatorEnergy { get; }
    public double PredationEnergyTransferRatio { get; }
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
    public double OrganismSensingRadius { get; }
    public double OrganismSteeringRate { get; }
    public double InitialSensingRadiusVariationAmplitude { get; }
    public double InitialSteeringRateVariationAmplitude { get; }
    public double MinSensingRadius { get; }
    public double MaxSensingRadius { get; }
    public double MinSteeringRate { get; }
    public double MaxSteeringRate { get; }
    public double SensingRadiusMutationAmplitude { get; }
    public double SteeringRateMutationAmplitude { get; }
    public bool EnableNeuralControl { get; }
    public double NeuralWeightMin { get; }
    public double NeuralWeightMax { get; }
    public double InitialNeuralWeightVariationAmplitude { get; }
    public double SpeedModulationAmplitude { get; }
    public double NeuralWeightMutationAmplitude { get; }

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
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount, initialFoodCount,
            organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius, foodRegenerationIntervalSeconds,
            maxFoodCount, reproductionEnergyThreshold, reproductionEnergyCost, offspringInitialEnergy,
            maxOrganismCount, initialSpeedVariationAmplitude, initialMetabolismVariationAmplitude,
            minSpeed, maxSpeed, minMetabolism, maxMetabolism, speedMutationAmplitude, metabolismMutationAmplitude,
            30, 2, 0, 0, 0, 100, 0, 5, 2, 0.1)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount, initialFoodCount,
            organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius, foodRegenerationIntervalSeconds,
            maxFoodCount, reproductionEnergyThreshold, reproductionEnergyCost, offspringInitialEnergy,
            maxOrganismCount, initialSpeedVariationAmplitude, initialMetabolismVariationAmplitude,
            minSpeed, maxSpeed, minMetabolism, maxMetabolism, speedMutationAmplitude, metabolismMutationAmplitude,
            organismSensingRadius, organismSteeringRate, initialSensingRadiusVariationAmplitude,
            initialSteeringRateVariationAmplitude, minSensingRadius, maxSensingRadius, minSteeringRate,
            maxSteeringRate, sensingRadiusMutationAmplitude, steeringRateMutationAmplitude, false, -1, 1, 0.5, 0.5)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude,
        bool enableNeuralControl, double neuralWeightMin, double neuralWeightMax,
        double initialNeuralWeightVariationAmplitude, double speedModulationAmplitude)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight, initialOrganismCount, initialFoodCount,
            organismSpeed, organismEnergyDrainPerSecond, foodPickupRadius, foodRegenerationIntervalSeconds,
            maxFoodCount, reproductionEnergyThreshold, reproductionEnergyCost, offspringInitialEnergy,
            maxOrganismCount, initialSpeedVariationAmplitude, initialMetabolismVariationAmplitude,
            minSpeed, maxSpeed, minMetabolism, maxMetabolism, speedMutationAmplitude, metabolismMutationAmplitude,
            organismSensingRadius, organismSteeringRate, initialSensingRadiusVariationAmplitude,
            initialSteeringRateVariationAmplitude, minSensingRadius, maxSensingRadius, minSteeringRate,
            maxSteeringRate, sensingRadiusMutationAmplitude, steeringRateMutationAmplitude, enableNeuralControl,
            neuralWeightMin, neuralWeightMax, initialNeuralWeightVariationAmplitude, speedModulationAmplitude, 0)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude,
        bool enableNeuralControl, double neuralWeightMin, double neuralWeightMax,
        double initialNeuralWeightVariationAmplitude, double speedModulationAmplitude,
        double neuralWeightMutationAmplitude)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight,
            initialOrganismCount, initialFoodCount, organismSpeed, organismEnergyDrainPerSecond,
            foodPickupRadius, foodRegenerationIntervalSeconds, maxFoodCount, reproductionEnergyThreshold,
            reproductionEnergyCost, offspringInitialEnergy, maxOrganismCount, initialSpeedVariationAmplitude,
            initialMetabolismVariationAmplitude, minSpeed, maxSpeed, minMetabolism,
            maxMetabolism, speedMutationAmplitude, metabolismMutationAmplitude, organismSensingRadius,
            organismSteeringRate, initialSensingRadiusVariationAmplitude, initialSteeringRateVariationAmplitude, minSensingRadius,
            maxSensingRadius, minSteeringRate, maxSteeringRate, sensingRadiusMutationAmplitude,
            steeringRateMutationAmplitude, enableNeuralControl, neuralWeightMin, neuralWeightMax,
            initialNeuralWeightVariationAmplitude, speedModulationAmplitude, neuralWeightMutationAmplitude, 0,
            100.0)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude,
        bool enableNeuralControl, double neuralWeightMin, double neuralWeightMax,
        double initialNeuralWeightVariationAmplitude, double speedModulationAmplitude,
        double neuralWeightMutationAmplitude, int initialPredatorCount, double initialPredatorEnergy)
        : this(
            seed, tickDurationSeconds, worldWidth, worldHeight,
            initialOrganismCount, initialFoodCount, organismSpeed, organismEnergyDrainPerSecond,
            foodPickupRadius, foodRegenerationIntervalSeconds, maxFoodCount, reproductionEnergyThreshold,
            reproductionEnergyCost, offspringInitialEnergy, maxOrganismCount, initialSpeedVariationAmplitude,
            initialMetabolismVariationAmplitude, minSpeed, maxSpeed, minMetabolism,
            maxMetabolism, speedMutationAmplitude, metabolismMutationAmplitude, organismSensingRadius,
            organismSteeringRate, initialSensingRadiusVariationAmplitude, initialSteeringRateVariationAmplitude, minSensingRadius,
            maxSensingRadius, minSteeringRate, maxSteeringRate, sensingRadiusMutationAmplitude,
            steeringRateMutationAmplitude, enableNeuralControl, neuralWeightMin, neuralWeightMax,
            initialNeuralWeightVariationAmplitude, speedModulationAmplitude, neuralWeightMutationAmplitude, initialPredatorCount,
            initialPredatorEnergy, 0.8)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude,
        bool enableNeuralControl, double neuralWeightMin, double neuralWeightMax,
        double initialNeuralWeightVariationAmplitude, double speedModulationAmplitude,
        double neuralWeightMutationAmplitude, int initialPredatorCount, double initialPredatorEnergy,
        double predationEnergyTransferRatio)
        : this(seed, tickDurationSeconds, worldWidth, worldHeight,
            initialOrganismCount, initialFoodCount, organismSpeed, organismEnergyDrainPerSecond,
            foodPickupRadius, foodRegenerationIntervalSeconds, maxFoodCount, reproductionEnergyThreshold,
            reproductionEnergyCost, offspringInitialEnergy, maxOrganismCount, initialSpeedVariationAmplitude,
            initialMetabolismVariationAmplitude, minSpeed, maxSpeed, minMetabolism, maxMetabolism,
            speedMutationAmplitude, metabolismMutationAmplitude, organismSensingRadius, organismSteeringRate,
            initialSensingRadiusVariationAmplitude, initialSteeringRateVariationAmplitude, minSensingRadius,
            maxSensingRadius, minSteeringRate, maxSteeringRate, sensingRadiusMutationAmplitude,
            steeringRateMutationAmplitude, enableNeuralControl, neuralWeightMin, neuralWeightMax,
            initialNeuralWeightVariationAmplitude, speedModulationAmplitude, neuralWeightMutationAmplitude,
            initialPredatorCount, initialPredatorEnergy, predationEnergyTransferRatio,
            8.0, 1.0, 0.0, 50.0, 0.0, 1.0, 0.0, 0.0, 1.0, 0.05)
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
        double speedMutationAmplitude, double metabolismMutationAmplitude,
        double organismSensingRadius, double organismSteeringRate,
        double initialSensingRadiusVariationAmplitude, double initialSteeringRateVariationAmplitude,
        double minSensingRadius, double maxSensingRadius, double minSteeringRate, double maxSteeringRate,
        double sensingRadiusMutationAmplitude, double steeringRateMutationAmplitude,
        bool enableNeuralControl, double neuralWeightMin, double neuralWeightMax,
        double initialNeuralWeightVariationAmplitude, double speedModulationAmplitude,
        double neuralWeightMutationAmplitude, int initialPredatorCount, double initialPredatorEnergy,
        double predationEnergyTransferRatio,
        double organismAttackRadius, double organismAttackEfficiency, double minAttackRadius, double maxAttackRadius,
        double minAttackEfficiency, double maxAttackEfficiency, double initialAttackRadiusVariationAmplitude,
        double initialAttackEfficiencyVariationAmplitude, double attackRadiusMutationAmplitude, double attackEfficiencyMutationAmplitude)
    {
        ValidateNonnegativeFinite(organismAttackRadius, nameof(organismAttackRadius));
        ValidateNonnegativeFinite(organismAttackEfficiency, nameof(organismAttackEfficiency));
        if (organismAttackEfficiency > 1) throw new ArgumentOutOfRangeException(nameof(organismAttackEfficiency));
        ValidateNonnegativeFinite(minAttackRadius, nameof(minAttackRadius));
        ValidateNonnegativeFinite(maxAttackRadius, nameof(maxAttackRadius));
        ValidateNonnegativeFinite(minAttackEfficiency, nameof(minAttackEfficiency));
        ValidateNonnegativeFinite(maxAttackEfficiency, nameof(maxAttackEfficiency));
        if (minAttackEfficiency > 1) throw new ArgumentOutOfRangeException(nameof(minAttackEfficiency));
        if (maxAttackEfficiency > 1) throw new ArgumentOutOfRangeException(nameof(maxAttackEfficiency));
        if (minAttackRadius >= maxAttackRadius) throw new ArgumentOutOfRangeException(nameof(maxAttackRadius));
        if (minAttackEfficiency >= maxAttackEfficiency) throw new ArgumentOutOfRangeException(nameof(maxAttackEfficiency));
        if (organismAttackRadius < minAttackRadius || organismAttackRadius > maxAttackRadius)
            throw new ArgumentOutOfRangeException(nameof(organismAttackRadius));
        if (organismAttackEfficiency < minAttackEfficiency || organismAttackEfficiency > maxAttackEfficiency)
            throw new ArgumentOutOfRangeException(nameof(organismAttackEfficiency));
        ValidateNonnegativeFinite(initialAttackRadiusVariationAmplitude, nameof(initialAttackRadiusVariationAmplitude));
        ValidateNonnegativeFinite(initialAttackEfficiencyVariationAmplitude, nameof(initialAttackEfficiencyVariationAmplitude));
        ValidateNonnegativeFinite(attackRadiusMutationAmplitude, nameof(attackRadiusMutationAmplitude));
        ValidateNonnegativeFinite(attackEfficiencyMutationAmplitude, nameof(attackEfficiencyMutationAmplitude));
        if (!double.IsFinite(predationEnergyTransferRatio) || predationEnergyTransferRatio < 0 || predationEnergyTransferRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(predationEnergyTransferRatio));
        ValidatePositiveFinite(tickDurationSeconds, nameof(tickDurationSeconds));
        ValidatePositiveFinite(worldWidth, nameof(worldWidth));
        ValidatePositiveFinite(worldHeight, nameof(worldHeight));
        ArgumentOutOfRangeException.ThrowIfNegative(initialOrganismCount);
        ArgumentOutOfRangeException.ThrowIfNegative(initialPredatorCount);
        ValidatePositiveFinite(initialPredatorEnergy, nameof(initialPredatorEnergy));
        if ((long)initialOrganismCount + initialPredatorCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(initialPredatorCount), "Combined initial population exceeds the organism-ID/array-length domain.");
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
        ValidateNonnegativeFinite(organismSensingRadius, nameof(organismSensingRadius));
        ValidateNonnegativeFinite(organismSteeringRate, nameof(organismSteeringRate));
        ValidateNonnegativeFinite(initialSensingRadiusVariationAmplitude, nameof(initialSensingRadiusVariationAmplitude));
        ValidateNonnegativeFinite(initialSteeringRateVariationAmplitude, nameof(initialSteeringRateVariationAmplitude));
        ValidateNonnegativeFinite(minSensingRadius, nameof(minSensingRadius));
        ValidateNonnegativeFinite(maxSensingRadius, nameof(maxSensingRadius));
        ValidateNonnegativeFinite(minSteeringRate, nameof(minSteeringRate));
        ValidateNonnegativeFinite(maxSteeringRate, nameof(maxSteeringRate));
        if (minSensingRadius >= maxSensingRadius) throw new ArgumentOutOfRangeException(nameof(maxSensingRadius));
        if (minSteeringRate >= maxSteeringRate) throw new ArgumentOutOfRangeException(nameof(maxSteeringRate));
        if (organismSensingRadius < minSensingRadius || organismSensingRadius > maxSensingRadius)
            throw new ArgumentOutOfRangeException(nameof(organismSensingRadius));
        if (organismSteeringRate < minSteeringRate || organismSteeringRate > maxSteeringRate)
            throw new ArgumentOutOfRangeException(nameof(organismSteeringRate));
        ValidateNonnegativeFinite(sensingRadiusMutationAmplitude, nameof(sensingRadiusMutationAmplitude));
        ValidateNonnegativeFinite(steeringRateMutationAmplitude, nameof(steeringRateMutationAmplitude));
        if (!double.IsFinite(neuralWeightMin) || neuralWeightMin < -NeuralGenome.MaxAbsoluteWeight || neuralWeightMin > 0)
            throw new ArgumentOutOfRangeException(nameof(neuralWeightMin));
        if (!double.IsFinite(neuralWeightMax) || neuralWeightMax > NeuralGenome.MaxAbsoluteWeight || neuralWeightMax < 0
            || neuralWeightMin >= neuralWeightMax)
            throw new ArgumentOutOfRangeException(nameof(neuralWeightMax));
        ValidateNonnegativeFinite(initialNeuralWeightVariationAmplitude, nameof(initialNeuralWeightVariationAmplitude));
        ValidateNonnegativeFinite(speedModulationAmplitude, nameof(speedModulationAmplitude));
        if (speedModulationAmplitude >= 1) throw new ArgumentOutOfRangeException(nameof(speedModulationAmplitude));
        ValidateNonnegativeFinite(neuralWeightMutationAmplitude, nameof(neuralWeightMutationAmplitude));

        OrganismAttackRadius = organismAttackRadius;
        OrganismAttackEfficiency = organismAttackEfficiency;
        MinAttackRadius = minAttackRadius;
        MaxAttackRadius = maxAttackRadius;
        MinAttackEfficiency = minAttackEfficiency;
        MaxAttackEfficiency = maxAttackEfficiency;
        InitialAttackRadiusVariationAmplitude = initialAttackRadiusVariationAmplitude;
        InitialAttackEfficiencyVariationAmplitude = initialAttackEfficiencyVariationAmplitude;
        AttackRadiusMutationAmplitude = attackRadiusMutationAmplitude;
        AttackEfficiencyMutationAmplitude = attackEfficiencyMutationAmplitude;
        Seed = seed;
        TickDurationSeconds = tickDurationSeconds;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        InitialOrganismCount = initialOrganismCount;
        InitialPredatorCount = initialPredatorCount;
        InitialPredatorEnergy = initialPredatorEnergy;
        PredationEnergyTransferRatio = predationEnergyTransferRatio;
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
        OrganismSensingRadius = organismSensingRadius; OrganismSteeringRate = organismSteeringRate;
        InitialSensingRadiusVariationAmplitude = initialSensingRadiusVariationAmplitude;
        InitialSteeringRateVariationAmplitude = initialSteeringRateVariationAmplitude;
        MinSensingRadius = minSensingRadius; MaxSensingRadius = maxSensingRadius;
        MinSteeringRate = minSteeringRate; MaxSteeringRate = maxSteeringRate;
        SensingRadiusMutationAmplitude = sensingRadiusMutationAmplitude;
        SteeringRateMutationAmplitude = steeringRateMutationAmplitude;
        EnableNeuralControl = enableNeuralControl;
        NeuralWeightMin = neuralWeightMin; NeuralWeightMax = neuralWeightMax;
        InitialNeuralWeightVariationAmplitude = initialNeuralWeightVariationAmplitude;
        SpeedModulationAmplitude = speedModulationAmplitude;
        NeuralWeightMutationAmplitude = neuralWeightMutationAmplitude;
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
