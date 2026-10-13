namespace EvoWorld.Core;

internal static class FoodRegenerationSchedule
{
    // Zero means the requested period exceeds the positive long tick range.
    // Inputs are validated positive finite doubles. Each normalized significand
    // has 53 bits; shifts up to 63 keep the exact quotient arithmetic in UInt128.
    internal static long Calculate(double interval, double timestep)
    {
        var intervalExponent = Math.ILogB(interval);
        var tickExponent = Math.ILogB(timestep);
        var shift = intervalExponent - tickExponent;
        if (shift < 0) return 1;
        if (shift >= 64) return 0;
        var numerator = (UInt128)(ulong)Math.ScaleB(interval, 52 - intervalExponent) << shift;
        var denominator = (ulong)Math.ScaleB(timestep, 52 - tickExponent);
        if (numerator > (UInt128)long.MaxValue * denominator) return 0;
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        if (remainder * 2 >= denominator) quotient++;
        return quotient == 0 ? 1 : checked((long)quotient);
    }
}
