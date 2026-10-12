using System.Numerics;

namespace EvoWorld.Core;

/// <summary>Bounded-work reflection of a single axis; no per-collision iteration.</summary>
internal static class BoundaryReflection
{
    internal static (double Position, bool Reflected) Reflect(double position, double displacement, double length)
    {
        var candidate = position + displacement;
        if (!double.IsFinite(candidate) || !double.IsFinite(displacement))
            throw new OverflowException("Movement candidate must be finite.");
        if (displacement == 0.0)
            return (position, false);
        if (candidate > 0.0 && candidate < length)
            return (candidate, false);

        // Each finite double is an integer significand times a power of two.
        // Align these *inputs*, not the rounded candidate, to an exact UInt128 grid.
        var start = Decompose(position);
        var travel = Decompose(Math.Abs(displacement));
        var dimension = Decompose(length);
        var exponent = Math.Min(travel.Exponent, dimension.Exponent);
        if (start.Significand != 0)
            exponent = Math.Min(exponent, start.Exponent);
        var x = Align(start, exponent);
        var d = Align(travel, exponent);
        var l = Align(dimension, exponent);
        // Each operand occupies <=126 bits: addition and 2*l fit in 127 bits.
        var period = l << 1;
        UInt128 remainder;
        if (displacement > 0.0)
            remainder = (x + d) % period;
        else if (d <= x)
            remainder = (x - d) % period;
        else
        {
            remainder = (d - x) % period;
            if (remainder != 0)
                remainder = period - remainder;
        }

        // At an exact endpoint, final direction must point inward. The sign of
        // travel decides whether that reverses the incoming velocity component.
        var reversed = remainder == 0 ? displacement < 0.0
            : remainder == l ? displacement > 0.0
            : remainder > l;
        var folded = remainder <= l ? remainder : period - remainder;
        var result = Math.ScaleB((double)folded, exponent);
        if (!double.IsFinite(result))
            throw new OverflowException("Reflected coordinate must be finite.");
        if (result >= length)
            result = Math.BitDecrement(length);
        return (result, reversed);
    }

    private static (ulong Significand, int Exponent) Decompose(double value)
    {
        if (value == 0.0)
            return (0, 0);
        var bits = BitConverter.DoubleToUInt64Bits(value);
        var encodedExponent = (int)((bits >> 52) & 0x7ff);
        var significand = bits & 0x000fffffffffffffUL;
        var exponent = -1074;
        if (encodedExponent != 0)
        {
            significand |= 1UL << 52;
            exponent = encodedExponent - 1023 - 52;
        }
        var trailing = BitOperations.TrailingZeroCount(significand);
        return (significand >> trailing, exponent + trailing);
    }

    private static UInt128 Align((ulong Significand, int Exponent) value, int exponent)
    {
        if (value.Significand == 0)
            return 0;
        var shift = value.Exponent - exponent;
        var bits = 64 - BitOperations.LeadingZeroCount(value.Significand);
        if (shift < 0 || bits + shift > 126)
            throw new OverflowException("Exact reflection exceeds the supported 126-bit input alignment.");
        return (UInt128)value.Significand << shift;
    }
}
