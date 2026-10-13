namespace EvoWorld.Core;

internal static class TraitVariation
{
    internal static double Apply(double center, double amplitude, double minimum, double maximum, uint output)
    {
        // Preserve the exact center when already in range. Custom adult values
        // outside mutation bounds remain unchanged as adults; offspring clamp.
        if (amplitude == 0) return Math.Clamp(center, minimum, maximum);
        var signed = 2.0 * (output / 4294967296.0) - 1.0;
        var candidate = center + signed * amplitude;
        // Finite nonnegative center and finite amplitude: any positive overflow
        // unambiguously exceeds the upper bound. No retries or extra draws.
        if (double.IsNaN(candidate)) throw new OverflowException("Trait variation must be defined.");
        return Math.Clamp(candidate, minimum, maximum);
    }
}
