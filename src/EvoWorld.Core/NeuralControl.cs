namespace EvoWorld.Core;

// Pure per-survivor controller. No RNG, persistent controller state, or publication.
internal static class NeuralControl
{
    internal static (double Heading, double EffectiveSpeed) Evaluate(Organism organism, double energy,
        IReadOnlyList<Food> food, bool[] consumed, SimulationSettings settings)
    {
        OrganismDiagnosticSample? ignored = null;
        return Evaluate(organism, energy, food, consumed, settings, -1, 0, ref ignored);
    }

    internal static (double Heading, double EffectiveSpeed) Evaluate(Organism organism, double energy,
        IReadOnlyList<Food> food, bool[] consumed, SimulationSettings settings,
        int trackedOrganismId, long tickCount, ref OrganismDiagnosticSample? diagnosticSample)
    {
        ValidateEnergy(energy);
        var target = TargetSensing.Resolve(organism, food, consumed, Array.Empty<Organism>());
        return Evaluate(organism, energy, target, settings, trackedOrganismId, tickCount, ref diagnosticSample);
    }

    internal static (double Heading, double EffectiveSpeed) Evaluate(Organism organism, double energy,
        SensedTarget target, SimulationSettings settings, int trackedOrganismId, long tickCount,
        ref OrganismDiagnosticSample? diagnosticSample)
    {
        OrganismResearchSample? ignored = null;
        return Evaluate(organism, energy, target, settings, trackedOrganismId, tickCount, ref diagnosticSample, false, ref ignored);
    }

    internal static (double Heading, double EffectiveSpeed) Evaluate(Organism organism, double energy,
        SensedTarget target, SimulationSettings settings, int trackedOrganismId, long tickCount,
        ref OrganismDiagnosticSample? diagnosticSample, bool captureResearch, ref OrganismResearchSample? researchSample)
    {
        Span<double> inputs = stackalloc double[NeuralGenome.InputCount];
        Span<double> outputs = stackalloc double[NeuralGenome.OutputCount];
        EncodeInputs(organism, energy, target, settings, inputs);
        organism.Brain.Forward(inputs, outputs);
        foreach (var output in outputs)
            if (!double.IsFinite(output) || output < -1 || output > 1)
                throw new OverflowException("Neural outputs must be finite and in [-1,1].");
        // Math.Tanh can round to +/-1. The strict settings bound still guarantees positivity.
        var multiplier = 1 + settings.SpeedModulationAmplitude * outputs[1];
        var effectiveSpeed = organism.Speed * multiplier;
        if (!double.IsFinite(multiplier) || multiplier <= 0 || !double.IsFinite(effectiveSpeed) || effectiveSpeed < 0)
            throw new OverflowException("Neural effective speed must be finite and nonnegative.");
        var duration = settings.TickDurationSeconds;
        var limit = duration > 1 && organism.SteeringRate > Math.PI / duration
            ? Math.PI : Math.Min(organism.SteeringRate * duration, Math.PI);
        var rotation = outputs[0] * limit;
        var heading = rotation == 0 ? organism.HeadingRadians : FoodSeeking.Normalize(organism.HeadingRadians + rotation);
        if (organism.Id == trackedOrganismId)
        {
            diagnosticSample = new(organism.Id, tickCount, true, true,
                outputs[0], outputs[1], rotation, effectiveSpeed, target.Kind);
            if (captureResearch && researchSample is not null)
                researchSample = researchSample with
                {
                    Inputs = new(inputs[0], inputs[1], inputs[2], inputs[3], inputs[4], inputs[5]),
                    Outputs = new(outputs[0], outputs[1]), AppliedRotation = rotation
                };
        }
        return (heading, effectiveSpeed);
    }

    internal static void EncodeInputs(Organism organism, double energy, IReadOnlyList<Food> food,
        bool[] consumed, SimulationSettings settings, Span<double> inputs)
    {
        ValidateEnergy(energy);
        var target = TargetSensing.Resolve(organism, food, consumed, Array.Empty<Organism>());
        EncodeInputs(organism, energy, target, settings, inputs);
    }

    internal static void EncodeInputs(Organism organism, double energy, SensedTarget target,
        SimulationSettings settings, Span<double> inputs)
    {
        ValidateEnergy(energy);
        inputs[0] = 0; inputs[1] = 1; inputs[2] = -1;
        var radius = FoodDistance.FromLength(organism.SensingRadius);
        if (organism.SensingRadius > 0 && target.Kind != TargetKind.None)
        {
            var dx = target.X - organism.X; var dy = target.Y - organism.Y;
            // Exact coordinate coincidence only; an underflowed distance ratio is not coincidence.
            if (dx == 0 && dy == 0) inputs[2] = 1;
            else
            {
                var targetHeading = FoodSeeking.Normalize(Math.Atan2(dy, dx));
                var angle = targetHeading - organism.HeadingRadians;
                if (angle > Math.PI) angle -= 2 * Math.PI;
                if (angle <= -Math.PI) angle += 2 * Math.PI;
                inputs[0] = Math.Sin(angle); inputs[1] = Math.Cos(angle);
                inputs[2] = 1 - 2 * ClampedRatio(FoodDistance.Between(organism.X, organism.Y, target.X, target.Y), radius);
            }
        }
        inputs[3] = 2 * Math.Min(energy / WorldInitializer.InitialOrganismEnergy, 1) - 1;
        inputs[4] = 2 * SpeedFraction(organism.Speed, settings.MinSpeed, settings.MaxSpeed) - 1;
        var wallDistance = Math.Min(Math.Min(organism.X, organism.Y),
            Math.Min(settings.WorldWidth - organism.X, settings.WorldHeight - organism.Y));
        inputs[5] = organism.SensingRadius > 0
            ? 1 - 2 * ClampedRatio(FoodDistance.FromLength(wallDistance), radius) : -1;
        foreach (var input in inputs)
            if (!double.IsFinite(input) || input < -1 || input > 1)
                throw new OverflowException("Neural inputs must be finite and in [-1,1].");
    }

    private static void ValidateEnergy(double energy)
    {
        if (!double.IsFinite(energy) || energy < 0)
            throw new OverflowException("Neural energy input must be finite and nonnegative.");
    }

    private static double ClampedRatio(FoodDistance numerator, FoodDistance denominator)
    {
        if (!double.IsFinite(numerator.Significand) || !double.IsFinite(denominator.Significand))
            throw new OverflowException("Neural distance ratios must be finite.");
        if (numerator.Significand == 0) return 0;
        if (numerator.CompareTo(denominator) >= 0) return 1;
        // Compare before division. Eligible distance never needs reconstruction,
        // even if its Euclidean magnitude would overflow. Tiny ratios may round to zero.
        var ratio = Math.ScaleB(numerator.Significand / denominator.Significand, numerator.Exponent - denominator.Exponent);
        if (!double.IsFinite(ratio)) throw new OverflowException("Neural distance ratios must be finite.");
        return Math.Clamp(ratio, 0, 1);
    }

    private static double SpeedFraction(double speed, double min, double max)
    {
        if (speed <= min) return 0;
        if (speed >= max) return 1;
        // Power-of-two scaling bounds intermediate differences and preserves
        // adjacent large bounds. Valid settings have min < max, both nonnegative.
        var exponent = Math.ILogB(max);
        var scaledMin = Math.ScaleB(min, -exponent);
        var ratio = (Math.ScaleB(speed, -exponent) - scaledMin) / (Math.ScaleB(max, -exponent) - scaledMin);
        if (!double.IsFinite(ratio)) throw new OverflowException("Neural speed fractions must be finite.");
        return Math.Clamp(ratio, 0, 1);
    }
}
