using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public class ViewportTransformTests
{
    [Theory]
    [InlineData(1000, 700, 1, 0, 0)]
    [InlineData(2000, 700, 1, 500, 0)]
    [InlineData(500, 700, 0.5, 0, 175)]
    public void FitsAndCentersWorld(double width, double height, float scale, float x, float y)
    {
        Assert.True(ViewportTransform.TryCreate(1000, 700, width, height, out var view));
        Assert.Equal(new ViewportTransform(scale, x, y), view);
    }

    [Theory]
    [InlineData(0, 700, 1000, 700)]
    [InlineData(1000, 700, 0, 700)]
    [InlineData(1000, 700, 1000, -1)]
    [InlineData(double.NaN, 700, 1000, 700)]
    [InlineData(1000, 700, double.PositiveInfinity, 700)]
    [InlineData(double.MaxValue, 700, 1000, 700)]
    [InlineData(double.Epsilon, double.Epsilon, 1000, 700)]
    [InlineData(1e-35, 1e-35, 1000, 700)]
    [InlineData(1000, double.Epsilon, 1000, 700)]
    public void UnrenderableDimensionsReturnSafeFallback(double width, double height, double vw, double vh)
    {
        Assert.False(ViewportTransform.TryCreate(width, height, vw, vh, out var view));
        Assert.Equal(default, view);
    }
}
