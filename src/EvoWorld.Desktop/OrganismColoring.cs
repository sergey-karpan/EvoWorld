using EvoWorld.Core;
using SkiaSharp;

namespace EvoWorld.Desktop;

internal enum ColorTrait { None, Speed, Metabolism, SensingRadius, SteeringRate, Energy }

internal static class OrganismColoring
{
    internal static double Value(Organism organism, ColorTrait trait) => trait switch
    {
        ColorTrait.Speed => organism.Speed,
        ColorTrait.Metabolism => organism.Metabolism,
        ColorTrait.SensingRadius => organism.SensingRadius,
        ColorTrait.SteeringRate => organism.SteeringRate,
        ColorTrait.Energy => organism.Energy,
        _ => 0
    };

    internal static (double Min, double Max) Bounds(WorldState world, PopulationStatistics statistics, ColorTrait trait)
    {
        var moments = trait switch
        {
            ColorTrait.Speed => statistics.Speed,
            ColorTrait.Metabolism => statistics.Metabolism,
            ColorTrait.SensingRadius => statistics.SensingRadius,
            ColorTrait.SteeringRate => statistics.SteeringRate,
            _ => default
        };
        if (trait != ColorTrait.Energy) return (moments.Min, moments.Max);
        if (world.Organisms.Count == 0) return (0, 0);
        var min = world.Organisms[0].Energy; var max = min;
        for (var i = 1; i < world.Organisms.Count; i++)
        {
            min = Math.Min(min, world.Organisms[i].Energy);
            max = Math.Max(max, world.Organisms[i].Energy);
        }
        return (min, max);
    }

    internal static SKColor Color(double value, double min, double max, ColorTrait trait)
    {
        if (trait == ColorTrait.None || max == min) return new SKColor(65, 201, 235);
        // All values are finite and nonnegative, so these differences cannot overflow.
        var ratio = Math.Clamp((value - min) / (max - min), 0, 1);
        return new SKColor((byte)(50 + 185 * ratio), 90, (byte)(235 - 185 * ratio));
    }
}
