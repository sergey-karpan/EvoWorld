using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class HistoryStatisticsTests
{
    [Fact]
    public void RuleBasedDiversityAddsZeroAllocationsBeyondTraitSamplingWhenWarmed()
    {
        Organism[] organisms = [Organism(Species.Herbivore, 7, 1, new NeuralGenome(Enumerable.Repeat(-8.0, 74).ToArray())),
            Organism(Species.Herbivore, 43, 3, new NeuralGenome(Enumerable.Repeat(8.0, 74).ToArray())),
            Organism(Species.Predator, 99, 2, new NeuralGenome(Enumerable.Repeat(-4.0, 74).ToArray())),
            Organism(Species.Predator, 123, 4, new NeuralGenome(Enumerable.Repeat(4.0, 74).ToArray()))];
        static SpeciesHistorySummary TraitsOnly(IReadOnlyList<Organism> population, Species species) => new(
            HistoryStatistics.Trait(population, species, static o => o.Speed),
            HistoryStatistics.Trait(population, species, static o => o.Metabolism),
            HistoryStatistics.Trait(population, species, static o => o.SensingRadius),
            HistoryStatistics.Trait(population, species, static o => o.SteeringRate), null);
        for (var i = 0; i < 1000; i++)
        {
            HistoryStatistics.Calculate(organisms, Species.Herbivore, false);
            HistoryStatistics.Calculate(organisms, Species.Predator, false);
            TraitsOnly(organisms, Species.Herbivore); TraitsOnly(organisms, Species.Predator);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { TraitsOnly(organisms, Species.Herbivore); TraitsOnly(organisms, Species.Predator); }
        var traitBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            HistoryStatistics.Calculate(organisms, Species.Herbivore, false);
            HistoryStatistics.Calculate(organisms, Species.Predator, false);
        }
        var ruleBasedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, ruleBasedBytes - traitBytes); // Existing trait enumeration is the control, not genome sampling.
        Assert.Equal(TraitsOnly(organisms, Species.Herbivore), HistoryStatistics.Calculate(organisms, Species.Herbivore, false));
        Assert.Equal(TraitsOnly(organisms, Species.Predator), HistoryStatistics.Calculate(organisms, Species.Predator, false));
        Assert.Equal(64.0, HistoryStatistics.Calculate(organisms, Species.Herbivore, true).MeanParameterVariance);
        Assert.Equal(16.0, HistoryStatistics.Calculate(organisms, Species.Predator, true).MeanParameterVariance);
    }

    public static IEnumerable<object[]> ExtremePopulations()
    {
        foreach (var species in new[] { Species.Herbivore, Species.Predator })
        {
            yield return [species, Array.Empty<double>()];
            yield return [species, new[] { double.MaxValue }];
            yield return [species, new[] { 0.0, double.MaxValue }];
            yield return [species, new[] { Math.BitDecrement(double.MaxValue), double.MaxValue }];
            yield return [species, new[] { double.Epsilon, 3 * double.Epsilon }];
            yield return [species, new[] { 0.0, 0.0, 0.0 }];
            yield return [species, new[] { 1e300, Math.BitIncrement(1e300), Math.BitIncrement(Math.BitIncrement(1e300)) }];
        }
    }

    private static Organism Organism(Species species, int id, double value, NeuralGenome? brain = null) =>
        new(id, 50, 40, 0, 100, value, value, value, value, brain ?? EvoWorld.Core.Organism.ZeroBrain, species);

    [Theory]
    [MemberData(nameof(ExtremePopulations))]
    public void SingleSpeciesSummariesMatchCoreExactlyForEquivalentPopulationSets(Species species, double[] values)
    {
        var organisms = values.Select((v, i) => Organism(species, i, v)).ToArray();
        var simulation = new Simulation(PredationFixture.Settings(herbivores: organisms.Length, predators: 0), new WorldState(100, 80, organisms, []));
        var core = simulation.GetStatistics(); var desktop = HistoryStatistics.Calculate(simulation.State.World.Organisms, species);
        var expected = new[] { core.Speed, core.Metabolism, core.SensingRadius, core.SteeringRate };
        var actual = new[] { desktop.Speed, desktop.Metabolism, desktop.SensingRadius, desktop.SteeringRate };
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].Count, actual[i].Count);
            Assert.Equal(new[] { expected[i].Min, expected[i].Max, expected[i].Mean, expected[i].StandardDeviation }.Select(BitConverter.DoubleToInt64Bits),
                new[] { actual[i].Min, actual[i].Max, actual[i].Mean, actual[i].StandardDeviation }.Select(BitConverter.DoubleToInt64Bits));
            Assert.True(double.IsFinite(actual[i].Mean)); Assert.True(double.IsFinite(actual[i].StandardDeviation));
        }
    }

    [Fact]
    public void MixedSpeciesUseIndependentScalarExpectationsAndNeverAggregateOtherSpecies()
    {
        Organism[] organisms = [Organism(Species.Herbivore, 7, 1), Organism(Species.Predator, 43, 10),
            Organism(Species.Herbivore, 99, 3), Organism(Species.Predator, 123, 14), Organism(Species.Herbivore, 999, 5)];
        var h = HistoryStatistics.Calculate(organisms, Species.Herbivore); var p = HistoryStatistics.Calculate(organisms, Species.Predator);
        // H: (1+3+5)/3=3, population variance=(4+0+4)/3.
        // P: (10+14)/2=12, population variance=(4+4)/2.
        foreach (var trait in new[] { h.Speed, h.Metabolism, h.SensingRadius, h.SteeringRate })
        {
            Assert.Equal(3, trait.Count); Assert.Equal(1, trait.Min); Assert.Equal(5, trait.Max); Assert.Equal(3, trait.Mean);
            NeuralFixture.Near(Math.Sqrt(8.0 / 3), trait.StandardDeviation);
        }
        foreach (var trait in new[] { p.Speed, p.Metabolism, p.SensingRadius, p.SteeringRate })
        {
            Assert.Equal(2, trait.Count); Assert.Equal(10, trait.Min); Assert.Equal(14, trait.Max); Assert.Equal(12, trait.Mean); Assert.Equal(2, trait.StandardDeviation);
        }
    }

    [Fact]
    public void EmptyAndSingleSpeciesDiversityHaveDistinctNullAndZeroSemantics()
    {
        Assert.Null(HistoryStatistics.Diversity([], Species.Herbivore));
        Organism[] organisms = [Organism(Species.Herbivore, 7, 3, new NeuralGenome(Enumerable.Repeat(8.0, 74).ToArray()))];
        Assert.Equal(0.0, HistoryStatistics.Diversity(organisms, Species.Herbivore));
        Assert.Null(HistoryStatistics.Diversity(organisms, Species.Predator));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(-8.0)]
    [InlineData(8.0)]
    public void IdenticalNonBinaryOrExtremeWeightsHaveExactZeroVariance(double weight)
    {
        var brain = new NeuralGenome(Enumerable.Repeat(weight, 74).ToArray());
        var organisms = Enumerable.Range(0, 3).Select(i => Organism(Species.Predator, i, 1, brain)).ToArray();
        Assert.Equal(0.0, HistoryStatistics.Diversity(organisms, Species.Predator));
    }

    [Theory]
    [InlineData(-8.0, 8.0, 64.0)]
    [InlineData(0.0, 2.0, 1.0)]
    [InlineData(-2.0, 4.0, 9.0)]
    public void IndependentTwoGenomeFixtureUsesPopulationVarianceAcrossAllParameters(double a, double b, double expected)
    {
        Organism[] organisms = [Organism(Species.Herbivore, 7, 1, new NeuralGenome(Enumerable.Repeat(a, 74).ToArray())),
            Organism(Species.Herbivore, 43, 1, new NeuralGenome(Enumerable.Repeat(b, 74).ToArray()))];
        Assert.Equal(expected, HistoryStatistics.Diversity(organisms, Species.Herbivore));
    }

    [Fact]
    public void OneVaryingParameterAndSpeciesSeparationMatchIndependentVarianceFixture()
    {
        var h = new double[74]; h[0] = 2;
        var p = new double[74]; p[73] = 4;
        Organism[] organisms = [Organism(Species.Herbivore, 7, 1), Organism(Species.Predator, 43, 1),
            Organism(Species.Herbivore, 99, 1, new NeuralGenome(h)), Organism(Species.Predator, 123, 1, new NeuralGenome(p))];
        NeuralFixture.Near(1.0 / 74, HistoryStatistics.Diversity(organisms, Species.Herbivore)!.Value);
        NeuralFixture.Near(4.0 / 74, HistoryStatistics.Diversity(organisms, Species.Predator)!.Value);
    }

    [Theory]
    [InlineData(false, 0.1)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.1)]
    public void BirthMutationDispersionIsDescriptiveAndInactiveInRuleBasedMode(bool neural, double mutation)
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 7, energy: 200)], neural: neural, cap: 2, mutation: mutation);
        var history = new PopulationHistory(simulation);
        for (var i = 0; i < 100; i++) { simulation.AdvanceTick(); history.Observe(simulation); }
        Assert.Equal(2, history[1].HerbivoreCount); Assert.Equal(neural, history.Metadata.NeuralGenomeActive);
        var child = simulation.State.World.Organisms[1];
        var expected = child.Brain.Weights.ToArray().Sum(w => w * w) / (4 * 74); // Parent is zero: two-vector variance w²/4.
        if (neural) NeuralFixture.Near(expected, history[1].Herbivores.MeanParameterVariance!.Value);
        else Assert.All(history, sample => { Assert.Null(sample.Herbivores.MeanParameterVariance); Assert.Null(sample.Predators.MeanParameterVariance); });
        if (!neural || mutation == 0) Assert.Equal(0.0, expected);
        else Assert.True(expected > 0);
    }

    [Fact]
    public void AdjacentBoundedGenomeWeightsRetainTheirSmallPositiveVariance()
    {
        var a = Math.BitDecrement(8.0); var b = 8.0;
        Organism[] organisms = [Organism(Species.Herbivore, 7, 1, new NeuralGenome(Enumerable.Repeat(a, 74).ToArray())),
            Organism(Species.Herbivore, 43, 1, new NeuralGenome(Enumerable.Repeat(b, 74).ToArray()))];
        var expected = (b - a) * (b - a) / 4; // Two equal-weight individuals: squared separation / 4.
        Assert.True(expected > 0); Assert.Equal(expected, HistoryStatistics.Diversity(organisms, Species.Herbivore));
    }
}
