using EvoWorld.Core.Random;

namespace EvoWorld.Core;

// Birth-only parameter variation. The caller supplies the tentative transaction RNG.
internal static class NeuralMutation
{
    internal static NeuralGenome Mutate(NeuralGenome parent, double amplitude, double lower, double upper, Pcg32 random)
    {
        if (!double.IsFinite(amplitude) || amplitude < 0)
            throw new OverflowException("Neural mutation amplitude must be finite and nonnegative.");
        if (amplitude == 0) return parent;
        Span<double> weights = stackalloc double[NeuralGenome.WeightCount];
        var inherited = parent.Weights;
        for (var i = 0; i < weights.Length; i++)
        {
            var signed = 2.0 * (random.NextUInt() / 4294967296.0) - 1.0;
            var delta = signed * amplitude;
            weights[i] = AddAndClamp(inherited[i], delta, lower, upper);
        }
        return new NeuralGenome(weights); // Fresh defensive ownership, even if all values are unchanged.
    }

    internal static double AddAndClamp(double parent, double delta, double lower, double upper)
    {
        if (!double.IsFinite(parent) || Math.Abs(parent) > NeuralGenome.MaxAbsoluteWeight
            || !double.IsFinite(delta) || !double.IsFinite(lower) || !double.IsFinite(upper)
            || lower < -NeuralGenome.MaxAbsoluteWeight || upper > NeuralGenome.MaxAbsoluteWeight || lower >= upper)
            throw new OverflowException("Neural mutation requires finite deltas and bounded parameters.");
        // Endpoint differences are within [-16,16]. Compare first, so a huge
        // finite delta saturates without ever forming an overflowing candidate.
        if (delta > 0 && delta >= upper - parent) return upper;
        if (delta < 0 && delta <= lower - parent) return lower;
        return Math.Clamp(parent + delta, lower, upper);
    }
}
