using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class TraitStatisticsTests
{
    [Fact]
    public void EmptyPopulationAndDefaultValueHaveExactlyZeroFields()
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values().GetStatistics()))
            Assert.Equal(default(TraitStatistics), trait);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    public void SingletonHasExactValueAndZeroDeviation(double value)
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(value).GetStatistics()))
        {
            Assert.Equal(1, trait.Count); Assert.Equal(value, trait.Min); Assert.Equal(value, trait.Max);
            Assert.Equal(value, trait.Mean); Assert.Equal(0, trait.StandardDeviation);
        }
    }

    [Fact]
    public void OneAndThreeHavePopulationDeviationOne()
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(1, 3).GetStatistics()))
        {
            Assert.Equal(2, trait.Count); Assert.Equal(1, trait.Min); Assert.Equal(3, trait.Max);
            Assert.Equal(2, trait.Mean); Assert.Equal(1, trait.StandardDeviation);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13.75)]
    [InlineData(double.MaxValue)]
    public void IdenticalPopulationHasExactZeroDeviation(double value)
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(value, value, value).GetStatistics()))
        {
            Assert.Equal(value, trait.Mean); Assert.Equal(0, trait.StandardDeviation); Assert.Equal(3, trait.Count);
        }
    }

    [Fact]
    public void LargeArithmeticSequenceMatchesIndependentClosedForm()
    {
        // Uniform integers 0..9999: mean=(n-1)/2, population variance=(n*n-1)/12.
        const int count = 10000;
        var statistics = StatisticsFixture.Values(Enumerable.Range(0, count).Select(i => (double)i).ToArray()).GetStatistics();
        foreach (var trait in StatisticsFixture.Traits(statistics))
        {
            Assert.Equal(count, trait.Count); Assert.Equal(0, trait.Min); Assert.Equal(count - 1, trait.Max);
            StatisticsFixture.Near(4999.5, trait.Mean);
            StatisticsFixture.Near(Math.Sqrt((100000000.0 - 1) / 12), trait.StandardDeviation);
        }
    }

    [Fact]
    public void AsymmetricMaximumFixtureMatchesIndependentAnalyticMoments()
    {
        // [0,0,M]: mean=M/3; variance=2*M^2/9, so sigma=M*(sqrt(2)/3).
        var statistics = StatisticsFixture.Values(0, 0, double.MaxValue).GetStatistics();
        foreach (var trait in StatisticsFixture.Traits(statistics))
        {
            Assert.Equal(0, trait.Min); Assert.Equal(double.MaxValue, trait.Max);
            StatisticsFixture.Near(double.MaxValue / 3, trait.Mean);
            StatisticsFixture.Near(double.MaxValue * (Math.Sqrt(2) / 3), trait.StandardDeviation);
        }
    }

    [Fact]
    public void AdjacentMaximumValuesRetainSpreadDespiteRoundedMean()
    {
        var lower = Math.BitDecrement(double.MaxValue);
        var statistics = StatisticsFixture.Values(lower, double.MaxValue).GetStatistics();
        foreach (var trait in StatisticsFixture.Traits(statistics))
        {
            Assert.InRange(trait.Mean, lower, double.MaxValue);
            Assert.Equal((double.MaxValue - lower) / 2, trait.StandardDeviation);
        }
    }

    [Fact]
    public void UnevenNearMaximumFixtureDoesNotLoseVarianceToMeanRounding()
    {
        var lower = Math.BitDecrement(double.MaxValue);
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(lower, lower, double.MaxValue).GetStatistics()))
            StatisticsFixture.Near((double.MaxValue - lower) * (Math.Sqrt(2) / 3), trait.StandardDeviation);
    }

    [Fact]
    public void SubnormalSpreadRemainsRepresentable()
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(0, 4 * double.Epsilon).GetStatistics()))
        {
            Assert.Equal(2 * double.Epsilon, trait.Mean); Assert.Equal(2 * double.Epsilon, trait.StandardDeviation);
        }
    }

    [Fact]
    public void WideDynamicRangeAndOverlappingLargeSamplesRemainFinite()
    {
        foreach (var trait in StatisticsFixture.Traits(StatisticsFixture.Values(double.Epsilon, 1, double.MaxValue / 2, double.MaxValue).GetStatistics()))
        {
            StatisticsFixture.Near(double.MaxValue * 0.375, trait.Mean);
            // [0,0,M/2,M] to binary64 precision: variance = 11*M^2/64.
            StatisticsFixture.Near(double.MaxValue * (Math.Sqrt(11) / 8), trait.StandardDeviation);
        }
    }

    [Fact]
    public void DistinctTraitsAreMappedToTheirOwnFields()
    {
        var simulation = TraitFixture.Create([new Organism(0, 1, 1, 0, 100, 2, 3, 5, 7), new Organism(9, 1, 1, 0, 100, 4, 9, 13, 17)]);
        var traits = StatisticsFixture.Traits(simulation.GetStatistics());
        Assert.Equal(new[] { 3.0, 6, 9, 12 }, traits.Select(t => t.Mean));
        Assert.Equal(new[] { 1.0, 3, 4, 5 }, traits.Select(t => t.StandardDeviation));
    }

    [Fact]
    public void StatisticsValueTypesHaveNoWritableProperties()
    {
        Assert.True(typeof(TraitStatistics).IsValueType); Assert.True(typeof(PopulationStatistics).IsValueType);
        Assert.All(typeof(TraitStatistics).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(PopulationStatistics).GetProperties(), p => Assert.Null(p.SetMethod));
    }
}
