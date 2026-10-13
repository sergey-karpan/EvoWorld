namespace EvoWorld.Core;

/// <summary>Immutable population moments. An empty population has five zero fields.</summary>
public readonly record struct TraitStatistics
{
    public double Min { get; }
    public double Max { get; }
    public double Mean { get; }
    public double StandardDeviation { get; }
    public int Count { get; }

    private TraitStatistics(double min, double max, double mean, double deviation, int count)
    {
        Min = min; Max = max; Mean = mean; StandardDeviation = deviation; Count = count;
    }

    internal static TraitStatistics Calculate(IReadOnlyList<Organism> organisms, Func<Organism, double> select)
    {
        var count = organisms.Count;
        if (count == 0) return default;
        var min = select(organisms[0]);
        var max = min;
        for (var i = 1; i < count; i++)
        {
            var value = select(organisms[i]);
            min = Math.Min(min, value); max = Math.Max(max, value);
        }
        if (min == max) return new(min, max, min, 0, count);

        // Nonnegative finite samples make max-min finite. Center before scaling
        // to preserve small spreads near MaxValue; normalized offsets lie in [0,1].
        // Three ordered passes, compensated sums, no unscaled squares or sums.
        var range = max - min;
        double sum = 0, correction = 0;
        for (var i = 0; i < count; i++)
            Add((select(organisms[i]) - min) / range, ref sum, ref correction);
        var normalizedMean = Math.Clamp(sum / count, 0, 1);
        sum = 0; correction = 0;
        for (var i = 0; i < count; i++)
        {
            var difference = (select(organisms[i]) - min) / range - normalizedMean;
            Add(difference * difference, ref sum, ref correction);
        }
        var mean = Math.Clamp(min + range * normalizedMean, min, max);
        var deviation = range * Math.Sqrt(Math.Clamp(sum / count, 0, 1));
        return new(min, max, mean, deviation, count);
    }

    private static void Add(double value, ref double sum, ref double correction)
    {
        var adjusted = value - correction;
        var next = sum + adjusted;
        correction = (next - sum) - adjusted;
        sum = next;
    }
}
