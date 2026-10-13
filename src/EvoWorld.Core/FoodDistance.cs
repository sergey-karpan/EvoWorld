namespace EvoWorld.Core;

// A nonnegative Euclidean distance stored as significand * 2^exponent.
// Keeping the exponent separate avoids both overflow and subnormal rounding
// of the final distance. Comparisons use binary64 Hypot precision, not epsilon.
internal readonly record struct FoodDistance(int Exponent, double Significand) : IComparable<FoodDistance>
{
    public static FoodDistance FromLength(double length)
    {
        if (length == 0) return default;
        var exponent = Math.ILogB(length);
        return new(exponent, Math.ScaleB(length, -exponent));
    }

    public static FoodDistance Between(double x, double y, double foodX, double foodY)
    {
        // Valid world coordinates are nonnegative finite values, so differences
        // cannot overflow. Scaling puts the larger component in [1,2).
        var dx = Math.Abs(x - foodX);
        var dy = Math.Abs(y - foodY);
        var largest = Math.Max(dx, dy);
        if (largest == 0) return default; // Only coincident coordinates have zero distance.
        var exponent = Math.ILogB(largest);
        var norm = double.Hypot(Math.ScaleB(dx, -exponent), Math.ScaleB(dy, -exponent));
        if (!double.IsFinite(norm)) throw new OverflowException("Food distance must be finite after scaling.");
        return norm >= 2 ? new(exponent + 1, norm / 2) : new(exponent, norm);
    }

    public int CompareTo(FoodDistance other)
    {
        if (Significand == 0 || other.Significand == 0)
            return Significand.CompareTo(other.Significand);
        var exponentComparison = Exponent.CompareTo(other.Exponent);
        return exponentComparison != 0 ? exponentComparison : Significand.CompareTo(other.Significand);
    }
}
