using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class TrajectoryDescriptorTests
{
    private static TrajectoryTurning Describe(params (double X, double Y)[] points) =>
        TrajectoryDescriptor.Calculate(points.Select((p, i) => new TrailPoint(i, p.X, p.Y)).ToArray());
    private static void Near(double expected, double? actual) { Assert.NotNull(actual); Assert.InRange(Math.Abs(actual.Value - expected), 0, 1e-14); }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InsufficientPositionsHaveNoInventedZeroMetric(int count)
    {
        var result = TrajectoryDescriptor.Calculate(Enumerable.Range(0, count).Select(i => new TrailPoint(i, i, 0)).ToArray());
        Assert.Equal(0, result.ValidTurnCount); Assert.Null(result.SignedMean); Assert.Null(result.MeanAbsolute); Assert.Null(result.Consistency);
        Assert.Contains("N/A", result.Format()); Assert.Contains("max 500 positions", result.Format());
    }

    [Fact]
    public void StraightPathHasValidZeroTurnsButUnavailableNonzeroDirectionConsistency()
    {
        var result = Describe((0, 0), (1, 0), (2, 0), (3, 0));
        Assert.Equal(2, result.ValidTurnCount); Assert.Equal(0.0, result.SignedMean); Assert.Equal(0.0, result.MeanAbsolute); Assert.Null(result.Consistency);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void IndependentlyKnownSquareTurnsRespectScreenCoordinates(double ySign)
    {
        var result = Describe((0, 0), (1, 0), (1, ySign), (0, ySign), (0, 0));
        Assert.Equal(3, result.ValidTurnCount); Near(ySign * Math.PI / 2, result.SignedMean);
        Near(Math.PI / 2, result.MeanAbsolute); Assert.Equal(1.0, result.Consistency);
    }

    [Fact]
    public void MixedSignsAndExactZerosUseTheSpecifiedNonzeroDenominator()
    {
        // East, east, south, east: [0, +pi/2, -pi/2].
        var result = Describe((0, 0), (1, 0), (2, 0), (2, 1), (3, 1));
        Assert.Equal(3, result.ValidTurnCount); Near(0, result.SignedMean); Near(Math.PI / 3, result.MeanAbsolute); Assert.Equal(0.5, result.Consistency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StationarySegmentsInvalidateAdjacentTurnsWithoutBridging(int stationaryAt)
    {
        (double X, double Y)[] points = [(0, 0), (1, 0), (1, 1)];
        var list = points.ToList(); list.Insert(stationaryAt + 1, list[stationaryAt]);
        var result = Describe(list.ToArray());
        Assert.Equal(stationaryAt == 1 ? 0 : 1, result.ValidTurnCount);
        if (stationaryAt == 1) Assert.Null(result.SignedMean); else Near(Math.PI / 2, result.SignedMean);
    }

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(1e-300)]
    [InlineData(1e300)]
    public void ExtremeFiniteDisplacementsNormalizeWithoutProductOverflowOrUnderflow(double scale)
    {
        var result = Describe((0, 0), (scale, 0), (scale, scale));
        Assert.Equal(1, result.ValidTurnCount); Near(Math.PI / 2, result.SignedMean); Assert.Equal(1.0, result.Consistency);
    }

    [Fact]
    public void SmallestNonzeroAngleIsNotRoundedAway()
    {
        var result = Describe((0, 0), (1, 0), (2, double.Epsilon));
        Assert.Equal(double.Epsilon, result.SignedMean); Assert.Equal(double.Epsilon, result.MeanAbsolute); Assert.Equal(1.0, result.Consistency);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfinitePositionsAreRejectedEvenWithInsufficientPoints(double invalid) =>
        Assert.Throws<InvalidOperationException>(() => Describe((invalid, 0)));

    [Fact]
    public void OverflowingStepIsExplicitlyRejected() =>
        Assert.Throws<InvalidOperationException>(() => Describe((-double.MaxValue, 0), (double.MaxValue, 0), (0, 0)));

    [Fact]
    public void ReflectionShapedCornersIncludeAbruptTurnsWithoutClaimingCircleDetection()
    {
        // Northeast to northwest to northeast; independent dot=0, cross=+/-2.
        var result = Describe((0, 1), (1, 0), (0, -1), (1, -2));
        Near(0, result.SignedMean); Near(Math.PI / 2, result.MeanAbsolute); Assert.Equal(0.5, result.Consistency);
        Assert.Contains("Includes boundary effects", result.Format()); Assert.Contains("missing ticks are not detected", result.Format());
    }

    [Fact]
    public void SelectionResetAndDeathFrozenTrailPreserveExistingStorageSemantics()
    {
        var simulation = LineageLedgerTests.At(0, ParentageTests.Organism(7)); var inspection = new OrganismInspection();
        inspection.Select(simulation, 7); inspection.Observe(LineageLedgerTests.At(1, ParentageTests.Organism(7, x: 51)));
        inspection.Observe(LineageLedgerTests.At(2, ParentageTests.Organism(7, x: 52)));
        var result = TrajectoryDescriptor.Calculate(inspection); Assert.Equal(1, result.ValidTurnCount);
        inspection.Observe(LineageLedgerTests.At(3)); Assert.False(inspection.IsAlive);
        Assert.Equal(result, TrajectoryDescriptor.Calculate(inspection));
        inspection.Clear(simulation); Assert.Equal(0, TrajectoryDescriptor.Calculate(inspection).ValidTurnCount);
        inspection.Select(simulation, 7); Assert.Null(TrajectoryDescriptor.Calculate(inspection).SignedMean);
    }
}
