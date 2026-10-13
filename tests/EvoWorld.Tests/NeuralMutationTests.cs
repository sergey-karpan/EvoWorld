using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralMutationTests
{
    [Fact]
    public void ZeroAmplitudeMutatorReturnsParentWithoutReadingRngOrClamping()
    {
        var parent = new NeuralGenome(Enumerable.Repeat(8.0, 74).ToArray());
        var random = new Pcg32(42); var before = random.CaptureState();
        Assert.Same(parent, NeuralMutation.Mutate(parent, 0, -1, 1, random));
        Assert.Equal(before, random.CaptureState());
    }

    [Theory]
    [InlineData(0.25, 0.125, -1, 1, 0.375)]
    [InlineData(-0.25, -0.125, -1, 1, -0.375)]
    [InlineData(0.75, 0.25, -1, 1, 1)]
    [InlineData(-0.75, -0.25, -1, 1, -1)]
    [InlineData(0.75, 0.5, -1, 1, 1)]
    [InlineData(-0.75, -0.5, -1, 1, -1)]
    [InlineData(8, double.MaxValue, -1, 1, 1)]
    [InlineData(-8, -double.MaxValue, -1, 1, -1)]
    [InlineData(-8, double.MaxValue, -1, 1, 1)]
    [InlineData(8, -double.MaxValue, -1, 1, -1)]
    [InlineData(-8, 9, -1, 1, 1)]
    [InlineData(8, -9, -1, 1, -1)]
    [InlineData(8, -8, -1, 1, 0)]
    [InlineData(-8, 8, -1, 1, 0)]
    [InlineData(8, 0, -1, 1, 1)]
    [InlineData(-8, 0, -1, 1, -1)]
    [InlineData(8, -0.5, -1, 1, 1)]
    [InlineData(-8, 0.5, -1, 1, -1)]
    [InlineData(1, double.Epsilon, -1, 1, 1)]
    [InlineData(-1, -double.Epsilon, -1, 1, -1)]
    public void ComparisonFirstAdditionMatchesManualBoundaryAndNormalCalculations(double parent, double delta,
        double lower, double upper, double expected)
    {
        // Literal inputs/expected outputs above are the complete manual reference.
        // In particular, 8+(-8)=0 proves the raw parent is not clamped first.
        Assert.Equal(expected, NeuralMutation.AddAndClamp(parent, delta, lower, upper));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidMutationInputsThrowRatherThanSubstituteWeights(double invalid)
    {
        Assert.Throws<OverflowException>(() => NeuralMutation.AddAndClamp(invalid, 0, -1, 1));
        Assert.Throws<OverflowException>(() => NeuralMutation.AddAndClamp(0, invalid, -1, 1));
        Assert.Throws<OverflowException>(() => NeuralMutation.AddAndClamp(0, 0, invalid, 1));
        Assert.Throws<OverflowException>(() => NeuralMutation.AddAndClamp(0, 0, -1, invalid));
    }

    [Theory]
    [InlineData(0.125)]
    [InlineData(1)]
    [InlineData(double.Epsilon)]
    public void ActiveMutationCreatesIndependentGenomeAndPreservesInRangeDifferenceBound(double amplitude)
    {
        var parent = new NeuralGenome(NeuralMutationGolden.ParentA); var before = parent.Weights.ToArray();
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(parent)], NeuralMutationFixture.Settings(amplitude));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) reference.NextUInt();
        var expected = NeuralMutationFixture.MutatedWeights(parent, amplitude, -1, 1, reference);
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.NotSame(parent, child.Brain); Assert.Same(parent, simulation.State.World.Organisms[0].Brain);
        NeuralMutationFixture.AssertBits(expected, child.Brain); Assert.Equal(before, parent.Weights.ToArray());
        for (var i = 0; i < 74; i++)
        {
            Assert.InRange(child.Brain.Weights[i], -1, 1);
            Assert.True(Math.Abs(child.Brain.Weights[i] - before[i]) <= amplitude + 2e-16);
        }
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }

    [Fact]
    public void DistinctChildrenMayHaveIdenticalWeightsWhileConsumingAllDraws()
    {
        var parent = new NeuralGenome(Enumerable.Repeat(1.0, 74).ToArray());
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(parent, energy: 160),
            NeuralMutationFixture.Parent(parent, id: 9, energy: 160, x: 60)],
            NeuralMutationFixture.Settings(double.Epsilon, organisms: 2, cap: 4));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 160; i++) reference.NextUInt();
        simulation.AdvanceTick(); var organisms = simulation.State.World.Organisms;
        Assert.NotSame(parent, organisms[2].Brain); Assert.NotSame(parent, organisms[3].Brain); Assert.NotSame(organisms[2].Brain, organisms[3].Brain);
        Assert.Equal(parent.Weights.ToArray(), organisms[2].Brain.Weights.ToArray());
        Assert.Equal(parent.Weights.ToArray(), organisms[3].Brain.Weights.ToArray()); Assert.Equal(reference.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(8)]
    public void CustomParentsOutsideProductionBoundsClampChildrenWithoutChangingParents(double weight)
    {
        var parent = new NeuralGenome(Enumerable.Repeat(weight, 74).ToArray());
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(parent)], NeuralMutationFixture.Settings(0.125));
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.All(child.Brain.Weights.ToArray(), w => Assert.Equal(Math.Sign(weight), w));
        Assert.All(parent.Weights.ToArray(), w => Assert.Equal(weight, w)); Assert.NotSame(parent, child.Brain);
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(8)]
    public void MaximumFiniteAmplitudeSaturatesBothDirectionsAndNeverOverflows(double parentWeight)
    {
        var parent = new NeuralGenome(Enumerable.Repeat(parentWeight, 74).ToArray());
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(parent)],
            NeuralMutationFixture.Settings(double.MaxValue, lower: -0.25, upper: 0.5));
        var reference = new Pcg32(simulation.RandomState); for (var i = 0; i < 6; i++) reference.NextUInt();
        var expected = new double[74];
        for (var i = 0; i < 74; i++)
        {
            var output = reference.NextUInt();
            // Independent sign-only oracle: any nonzero signed draw times MaxValue
            // dominates an eight-unit parent. The midpoint leaves the raw parent.
            expected[i] = output < 2147483648U ? -0.25 : output > 2147483648U ? 0.5 : Math.Clamp(parentWeight, -0.25, 0.5);
        }
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.Contains(-0.25, expected); Assert.Contains(0.5, expected);
        NeuralMutationFixture.AssertBits(expected, child.Brain); Assert.Equal(reference.CaptureState(), simulation.RandomState);
        Assert.All(parent.Weights.ToArray(), w => Assert.Equal(parentWeight, w));
    }

    [Fact]
    public void ChildFirstEvaluatesMutatedBrainOnFollowingTick()
    {
        var parent = NeuralGenome.CreateZero();
        var original = new Organism(0, 50, 40, 0.25, 160, 0, 1, 30, 1, parent);
        var simulation = NeuralMutationFixture.Create([original], NeuralMutationFixture.Settings(cap: 2));
        simulation.AdvanceTick(); var child = simulation.State.World.Organisms[1];
        Assert.NotSame(parent, child.Brain); Assert.Equal(50, child.Energy);
        var inputs = NeuralFixture.Inputs(child, settings: simulation.Settings, energy: 49);
        var outputs = new double[2]; child.Brain.Forward(inputs, outputs);
        simulation.AdvanceTick(); var next = simulation.State.World.Organisms[1];
        NeuralFixture.Near(FoodSeeking.Normalize(child.HeadingRadians + outputs[0]), next.HeadingRadians);
        Assert.Equal(49, next.Energy); Assert.Same(child.Brain, next.Brain);
        Assert.Equal((child.X, child.Y), (next.X, next.Y));
    }
}
