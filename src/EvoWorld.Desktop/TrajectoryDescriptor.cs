using System.Globalization;

namespace EvoWorld.Desktop;

internal readonly record struct TrajectoryTurning(int ValidTurnCount, double? SignedMean, double? MeanAbsolute, double? Consistency)
{
    internal string Format() => "Observed sampled-path turning (retained selection trail; max 500 positions): "
        + string.Create(CultureInfo.InvariantCulture, $"{ValidTurnCount} valid turns | Signed mean {Value(SignedMean)} rad | Mean absolute {Value(MeanAbsolute)} rad | Direction consistency {Value(Consistency)}")
        + "\nNeighboring stored observations; missing ticks are not detected. Includes boundary effects; sparse sampling may alias loops.";
    private static string Value(double? value) => value?.ToString("G5", CultureInfo.InvariantCulture) ?? "N/A";
}

internal static class TrajectoryDescriptor
{
    internal static TrajectoryTurning Calculate(OrganismInspection inspection) => Calculate(inspection.Count, i => inspection[i]);
    internal static TrajectoryTurning Calculate(IReadOnlyList<TrailPoint> trail) => Calculate(trail.Count, i => trail[i]);

    private static TrajectoryTurning Calculate(int count, Func<int, TrailPoint> point)
    {
        double previousX = 0, previousY = 0, signed = 0, absolute = 0;
        var previousValid = false;
        var turns = 0; var positive = 0; var negative = 0;
        for (var i = 0; i < count; i++)
        {
            var current = point(i);
            if (!double.IsFinite(current.X) || !double.IsFinite(current.Y)) throw new InvalidOperationException("Nonfinite trajectory position.");
            if (i == 0) continue;
            var prior = point(i - 1);
            var dx = current.X - prior.X; var dy = current.Y - prior.Y;
            if (!double.IsFinite(dx) || !double.IsFinite(dy)) throw new InvalidOperationException("Nonfinite trajectory displacement.");
            var scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (scale == 0) { previousValid = false; continue; } // No bridging across stationary segments.
            dx /= scale; dy /= scale; // Subnormal steps stay nonzero; products cannot overflow.
            var length = double.Hypot(dx, dy);
            dx /= length; dy /= length;
            if (previousValid)
            {
                // X-right, Y-down: a positive signed angle is clockwise on screen.
                var angle = Math.Atan2(previousX * dy - previousY * dx, previousX * dx + previousY * dy);
                signed += angle; absolute += Math.Abs(angle); turns++;
                if (angle > 0) positive++; else if (angle < 0) negative++;
            }
            previousX = dx; previousY = dy; previousValid = true;
        }
        return new(turns, turns == 0 ? null : signed / turns, turns == 0 ? null : absolute / turns,
            positive + negative == 0 ? null : (double)Math.Max(positive, negative) / (positive + negative));
    }
}
