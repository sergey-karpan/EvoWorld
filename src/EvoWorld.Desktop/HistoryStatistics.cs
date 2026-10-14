using EvoWorld.Core;

namespace EvoWorld.Desktop;

internal readonly record struct HistoryTraitSummary(int Count, double Min, double Max, double Mean, double StandardDeviation);
internal readonly record struct SpeciesHistorySummary(HistoryTraitSummary Speed, HistoryTraitSummary Metabolism,
    HistoryTraitSummary SensingRadius, HistoryTraitSummary SteeringRate, double? MeanParameterVariance);

// Ordered scalar observations only. No genomes are copied and no controller is evaluated.
internal static class HistoryStatistics
{
    internal static SpeciesHistorySummary Calculate(IReadOnlyList<Organism> organisms, Species species, bool neuralGenomeActive = true) => new(
        Trait(organisms, species, static o => o.Speed), Trait(organisms, species, static o => o.Metabolism),
        Trait(organisms, species, static o => o.SensingRadius), Trait(organisms, species, static o => o.SteeringRate),
        neuralGenomeActive ? Diversity(organisms, species) : null);

    internal static HistoryTraitSummary Trait(IReadOnlyList<Organism> organisms, Species species, Func<Organism, double> select)
    {
        var count = 0;
        var min = double.MaxValue; var max = 0.0;
        foreach (var organism in organisms)
        {
            if (organism.Species != species) continue;
            var value = select(organism);
            if (!double.IsFinite(value) || value < 0) throw new InvalidOperationException("History traits must be finite and nonnegative.");
            min = Math.Min(min, value); max = Math.Max(max, value); count++;
        }
        if (count == 0) return default;
        if (min == max) return new(count, min, max, min, 0);
        // Mirror Core's three ordered passes, centering/scaling and compensated sums.
        // Filtering preserves the snapshot's order; no overflowing raw sums or squares.
        var range = max - min;
        double sum = 0, correction = 0;
        foreach (var organism in organisms)
            if (organism.Species == species) Add((select(organism) - min) / range, ref sum, ref correction);
        var mean = Math.Clamp(sum / count, 0, 1);
        sum = 0; correction = 0;
        foreach (var organism in organisms)
        {
            if (organism.Species != species) continue;
            var difference = (select(organism) - min) / range - mean;
            Add(difference * difference, ref sum, ref correction);
        }
        return new(count, min, max, Math.Clamp(min + range * mean, min, max),
            range * Math.Sqrt(Math.Clamp(sum / count, 0, 1)));
    }

    internal static double? Diversity(IReadOnlyList<Organism> organisms, Species species)
    {
        var count = 0;
        foreach (var organism in organisms) if (organism.Species == species) count++;
        if (count == 0) return null;
        if (count == 1) return 0;
        double total = 0, totalCorrection = 0;
        for (var parameter = 0; parameter < NeuralGenome.WeightCount; parameter++)
        {
            var min = double.PositiveInfinity; var max = double.NegativeInfinity;
            foreach (var organism in organisms)
                if (organism.Species == species)
                {
                    var value = organism.Brain.Weights[parameter];
                    min = Math.Min(min, value); max = Math.Max(max, value);
                }
            if (min == max) continue; // Identical weights have exact zero variance.
            var range = max - min; // At most 16; bounded signed weights cannot overflow.
            double sum = 0, correction = 0;
            foreach (var organism in organisms)
                if (organism.Species == species) Add((organism.Brain.Weights[parameter] - min) / range, ref sum, ref correction);
            var mean = sum / count;
            sum = 0; correction = 0;
            foreach (var organism in organisms)
            {
                if (organism.Species != species) continue;
                var difference = (organism.Brain.Weights[parameter] - min) / range - mean;
                Add(difference * difference, ref sum, ref correction);
            }
            Add(range * range * (sum / count), ref total, ref totalCorrection);
        }
        var result = total / NeuralGenome.WeightCount;
        if (!double.IsFinite(result) || result < 0) throw new InvalidOperationException("Invalid genome diversity.");
        return result;
    }

    private static void Add(double value, ref double sum, ref double correction)
    {
        var adjusted = value - correction;
        var next = sum + adjusted;
        correction = (next - sum) - adjusted;
        sum = next;
    }
}
