using EvoWorld.Core;

namespace EvoWorld.Desktop;

internal enum ControllerMode { RuleBased, Neural }

internal readonly record struct ExperimentOptions(ControllerMode Controller, double MutationAmplitude)
{
    internal static ExperimentOptions Initial => new(ControllerMode.RuleBased, 0.1);
    internal ExperimentOptions Effective => Controller == ControllerMode.Neural ? this : new(Controller, 0);
}

internal static class ExperimentSettings
{
    internal static ReadOnlySpan<double> MutationChoices => [0, 0.05, 0.1, 0.5];

    internal static void Validate(ExperimentOptions options)
    {
        if (!Enum.IsDefined(options.Controller)) throw new ArgumentOutOfRangeException(nameof(options));
        if (!MutationChoices.Contains(options.MutationAmplitude)) throw new ArgumentOutOfRangeException(nameof(options));
    }

    internal static SimulationSettings Create(ExperimentOptions options)
    {
        Validate(options);
        var baseline = SimulationSettings.CreateDefault();
        var neural = options.Controller == ControllerMode.Neural;
        if (neural && baseline.InitialNeuralWeightVariationAmplitude <= 0)
            throw new InvalidOperationException("The neural demonstration requires positive Core initial weight variation.");
        // Core configuration is immutable and uses positional constructors. Keep
        // this Desktop-only mapping explicit, centralized, and tested property by
        // property. Only controller enable and birth-time mutation are overridden.
        return new SimulationSettings(baseline.Seed, baseline.TickDurationSeconds, baseline.WorldWidth, baseline.WorldHeight,
            baseline.InitialOrganismCount, baseline.InitialFoodCount, baseline.OrganismSpeed,
            baseline.OrganismEnergyDrainPerSecond, baseline.FoodPickupRadius, baseline.FoodRegenerationIntervalSeconds,
            baseline.MaxFoodCount, baseline.ReproductionEnergyThreshold, baseline.ReproductionEnergyCost,
            baseline.OffspringInitialEnergy, baseline.MaxOrganismCount, baseline.InitialSpeedVariationAmplitude,
            baseline.InitialMetabolismVariationAmplitude, baseline.MinSpeed, baseline.MaxSpeed, baseline.MinMetabolism,
            baseline.MaxMetabolism, baseline.SpeedMutationAmplitude, baseline.MetabolismMutationAmplitude,
            baseline.OrganismSensingRadius, baseline.OrganismSteeringRate, baseline.InitialSensingRadiusVariationAmplitude,
            baseline.InitialSteeringRateVariationAmplitude, baseline.MinSensingRadius, baseline.MaxSensingRadius,
            baseline.MinSteeringRate, baseline.MaxSteeringRate, baseline.SensingRadiusMutationAmplitude,
            baseline.SteeringRateMutationAmplitude, neural, baseline.NeuralWeightMin, baseline.NeuralWeightMax,
            baseline.InitialNeuralWeightVariationAmplitude, baseline.SpeedModulationAmplitude, options.Effective.MutationAmplitude);
    }
}
