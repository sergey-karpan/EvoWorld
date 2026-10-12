using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class BoundaryReflectionTests
{
    // Integer fixtures derived directly from mirrored intervals of length 10.
    [Theory]
    [InlineData(2, 3, 5, false)]
    [InlineData(2, -3, 1, true)]
    [InlineData(8, 5, 7, true)]
    [InlineData(2, -2, 0, true)]
    [InlineData(0, -2, 2, true)]
    [InlineData(0, 2, 2, false)]
    [InlineData(0, 0, 0, false)]
    [InlineData(2, 18, 0, false)]
    [InlineData(2, -22, 0, true)]
    [InlineData(2, 25, 7, false)]
    [InlineData(2, -25, 3, true)]
    [InlineData(2, 1005, 7, false)]
    [InlineData(2, -1005, 3, true)]
    public void MirroredIntervalsHaveExpectedPositionAndParity(
        double start, double travel, double expected, bool reversed)
    {
        Assert.Equal((expected, reversed), BoundaryReflection.Reflect(start, travel, 10));
    }

    [Theory]
    [InlineData(2, 8, true)]
    [InlineData(2, -12, false)]
    [InlineData(2, 1008, true)]
    public void ExactUpperHitsCorrectPositionButKeepInwardDirection(double start, double travel, bool reversed)
    {
        Assert.Equal((Math.BitDecrement(10.0), reversed), BoundaryReflection.Reflect(start, travel, 10));
    }

    [Fact]
    public void RoundedCandidateDoesNotInventWallCollision()
    {
        var start = Math.BitDecrement(1.0);
        var displacement = Math.ScaleB(1.0, -54);
        Assert.Equal(1.0, start + displacement); // Rounded sum looks like a hit.
        Assert.Equal((start, false), BoundaryReflection.Reflect(start, displacement, 1.0));
    }

    [Fact]
    public void LargeDisplacementPreservesStartInformationLostByFloatingAddition()
    {
        // Exact integers: (1 + 2^53) % 20 = 13, triangle wave => 7, reversed.
        Assert.Equal((7.0, true), BoundaryReflection.Reflect(1, 9007199254740992.0, 10));
    }

    [Fact]
    public void MaximumDimensionDoesNotOverflowItsConceptualPeriod()
    {
        Assert.Equal((Math.BitDecrement(double.MaxValue), true),
            BoundaryReflection.Reflect(0, double.MaxValue, double.MaxValue));
        Assert.Equal((1.0, false), BoundaryReflection.Reflect(0, 1, double.MaxValue));
    }

    [Fact]
    public void SmallestDimensionHasRepresentablePositionAndCorrectParity()
    {
        Assert.Equal((0.0, true), BoundaryReflection.Reflect(0, double.Epsilon, double.Epsilon));
        Assert.Equal((0.0, false), BoundaryReflection.Reflect(0, -double.Epsilon, double.Epsilon));
        Assert.Equal((0.0, false), BoundaryReflection.Reflect(0, 2 * double.Epsilon, double.Epsilon));
    }

    [Fact]
    public void UnsupportedExactAlignmentThrowsInsteadOfGuessingParity()
    {
        Assert.Throws<OverflowException>(() => BoundaryReflection.Reflect(0, 1, double.Epsilon));
    }

    [Fact]
    public void ExactAlignmentLimitIsExplicitAndIndependentOfBounceCount()
    {
        // 2^125 complete axis lengths: exact endpoint at zero, even parity.
        Assert.Equal((0.0, false), BoundaryReflection.Reflect(0, 1, Math.ScaleB(1.0, -125)));
        Assert.Throws<OverflowException>(() => BoundaryReflection.Reflect(0, 1, Math.ScaleB(1.0, -126)));
    }

    [Fact]
    public void NonfiniteCandidatesAndDisplacementsAreRejected()
    {
        Assert.Throws<OverflowException>(() => BoundaryReflection.Reflect(double.MaxValue / 2, double.MaxValue, double.MaxValue));
        Assert.Throws<OverflowException>(() => BoundaryReflection.Reflect(0, double.PositiveInfinity, 10));
        Assert.Throws<OverflowException>(() => BoundaryReflection.Reflect(0, double.NaN, 10));
    }
}
