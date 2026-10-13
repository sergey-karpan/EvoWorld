using System.Reflection;
using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public class NeuralGenomeTests
{
    // Independent tanh fixtures allow an absolute 2e-15 rounding tolerance.
    private const double Tolerance = 2e-15;

    [Fact]
    public void FixedTopologyAndPublicReadOnlyViewMatchContract()
    {
        Assert.Equal(6, NeuralGenome.InputCount); Assert.Equal(8, NeuralGenome.HiddenCount);
        Assert.Equal(2, NeuralGenome.OutputCount); Assert.Equal(74, NeuralGenome.WeightCount);
        Assert.Equal(8, NeuralGenome.MaxAbsoluteWeight); Assert.True(typeof(NeuralGenome).IsSealed);
        var property = typeof(NeuralGenome).GetProperty(nameof(NeuralGenome.Weights))!;
        Assert.Equal(typeof(ReadOnlySpan<double>), property.PropertyType); Assert.Null(property.SetMethod);
        Assert.Empty(typeof(NeuralGenome).GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(73)]
    [InlineData(75)]
    [InlineData(148)]
    public void IncorrectParameterCountsAreRejected(int count)
    {
        Assert.Equal("weights", Assert.Throws<ArgumentException>(() => new NeuralGenome(new double[count])).ParamName);
    }

    public static IEnumerable<object[]> InvalidParameters()
    {
        foreach (var index in new[] { 0, 48, 73 })
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                Math.BitIncrement(8.0), Math.BitDecrement(-8.0), double.MaxValue, -double.MaxValue })
                yield return [index, value];
    }

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public void InvalidWeightsAndBiasesAreRejectedWithoutClamping(int index, double value)
    {
        var weights = new double[74]; weights[index] = value;
        var exception = Assert.Throws<ArgumentException>(() => new NeuralGenome(weights));
        Assert.Equal("weights", exception.ParamName); Assert.Contains($"index {index}", exception.Message);
    }

    [Fact]
    public void DefensiveCopiesPreserveValuesAndIndependentOwnership()
    {
        var source = DenseWeights(); var expected = (double[])source.Clone();
        var first = new NeuralGenome(source); var second = new NeuralGenome(source);
        var firstOutput = Evaluate(first, DenseInputs()); var secondOutput = Evaluate(second, DenseInputs());
        Array.Fill(source, 8);
        var exportedCopy = first.Weights.ToArray(); Array.Fill(exportedCopy, -8);
        Assert.Equal(expected, first.Weights.ToArray()); Assert.Equal(expected, second.Weights.ToArray());
        AssertBits(firstOutput, Evaluate(first, DenseInputs())); AssertBits(secondOutput, Evaluate(second, DenseInputs()));
        var storage = typeof(NeuralGenome).GetField("_weights", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(storage.IsPrivate && storage.IsInitOnly);
        Assert.NotSame(source, storage.GetValue(first)); Assert.NotSame(storage.GetValue(first), storage.GetValue(second));
    }

    [Theory]
    [InlineData(-8)]
    [InlineData(8)]
    public void ExactWeightBoundsAreAcceptedAndSaturateSafely(double value)
    {
        var genome = new NeuralGenome(Enumerable.Repeat(value, 74).ToArray());
        Assert.All(genome.Weights.ToArray(), w => Assert.Equal(value, w));
        var outputs = Evaluate(genome, [1, 1, 1, 1, 1, 1]);
        Assert.Equal(1, outputs[0]); Assert.Equal(1, outputs[1]);
    }

    [Fact]
    public void OppositeBoundOutputRowsSaturateAtPositiveAndNegativeOne()
    {
        var weights = Enumerable.Repeat(8.0, 74).ToArray();
        for (var index = 64; index < 72; index++) weights[index] = -8;
        weights[73] = -8;
        AssertBits([1, -1], Evaluate(new NeuralGenome(weights), [1, 1, 1, 1, 1, 1]));
    }

    [Fact]
    public void ZeroGenomeProducesPositiveBitwiseZeroForValidBoundaryAndMixedInputs()
    {
        var genome = NeuralGenome.CreateZero();
        Assert.Equal(74, genome.Weights.Length);
        Assert.All(genome.Weights.ToArray(), w => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(w)));
        foreach (var inputs in new double[][] { [0, 0, 0, 0, 0, 0], [-1, -1, -1, -1, -1, -1],
            [1, 1, 1, 1, 1, 1], [-1, 1, -0.25, 0.5, double.Epsilon, -double.Epsilon] })
            Assert.All(Evaluate(genome, inputs), o => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(o)));
    }

    [Fact]
    public void HandDerivedMultipleHiddenNeuronsFeedBothOutputChannels()
    {
        var weights = new double[74];
        weights[0] = 0.5; weights[1] = -0.25; weights[48] = 0.1;
        weights[8] = 0.75; weights[11] = -0.5; weights[49] = -0.2;
        weights[15] = -0.125; weights[16] = 0.25; weights[50] = 0.05;
        weights[18] = -0.5; weights[23] = 0.25; weights[51] = -0.1;
        weights[56] = 1.25; weights[57] = -0.75; weights[58] = 0.5; weights[59] = 0.25; weights[72] = -0.15;
        weights[64] = -0.5; weights[65] = 1.5; weights[66] = -1; weights[67] = 0.75; weights[73] = 0.2;
        // By hand: hidden pre-activations = [0.3,0.75,0.4,-0.45,0,0,0,0].
        // Evaluating the two explicit weighted tanh expressions in a separate
        // PowerShell script gave the constants below, without calling Core.
        var outputs = Evaluate(new NeuralGenome(weights), [0.2, -0.4, 0.6, -0.8, 1, -1]);
        Near(-0.17587346158625683, outputs[0]); Near(0.30106823796174914, outputs[1]);
    }

    [Fact]
    public void DenseIndependentReferenceCoversEveryWeightAndBias()
    {
        // Separate PowerShell reference: p[j]=(j%17-8)/16; materialize each
        // six-input row and each eight-hidden row, sum their products, add biases,
        // then tanh. Dyadic products make this an independent indexing fixture.
        var outputs = Evaluate(new NeuralGenome(DenseWeights()), DenseInputs());
        Near(-0.46687760837527126, outputs[0]); Near(0.47615711535949956, outputs[1]);
    }

    public static IEnumerable<object[]> ParameterIndices() => Enumerable.Range(0, 74).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(ParameterIndices))]
    public void EveryParameterFollowsStableRowMajorLayout(int index)
    {
        // All four block offsets are exercised: IH=0, hidden biases=48,
        // HO=56, output biases=72. Probe each of the 74 slots independently.
        var weights = new double[74]; var inputs = new double[6];
        double[] expected = [0, 0]; weights[index] = 0.5;
        if (index < 56)
        {
            var h = index < 48 ? index / 6 : index - 48;
            if (index < 48) inputs[index % 6] = 1;
            weights[56 + h] = 1; weights[64 + h] = -1;
            expected[0] = Math.Tanh(Math.Tanh(0.5)); expected[1] = -expected[0];
        }
        else if (index < 72)
        {
            weights[48 + (index - 56) % 8] = 0.5;
            expected[(index - 56) / 8] = Math.Tanh(0.5 * Math.Tanh(0.5));
        }
        else expected[index - 72] = Math.Tanh(0.5);
        var outputs = Evaluate(new NeuralGenome(weights), inputs);
        Near(expected[0], outputs[0]); Near(expected[1], outputs[1]);
    }

    [Fact]
    public void ZeroInputsStillApplyHiddenAndOutputBiases()
    {
        var weights = new double[74]; weights[48] = 0.5; weights[55] = -0.25;
        weights[56] = 2; weights[63] = -1; weights[64] = -1; weights[71] = 0.5;
        weights[72] = 0.125; weights[73] = -0.25;
        var outputs = Evaluate(new NeuralGenome(weights), new double[6]);
        Near(Math.Tanh(0.125 + 2 * Math.Tanh(0.5) - Math.Tanh(-0.25)), outputs[0]);
        Near(Math.Tanh(-0.25 - Math.Tanh(0.5) + 0.5 * Math.Tanh(-0.25)), outputs[1]);
    }

    [Fact]
    public void HiddenSummationStartsWithBiasAndUsesAscendingInputOrder()
    {
        var weights = new double[74]; weights[48] = 8; weights[0] = -8;
        var tiny = Math.ScaleB(1, -52);
        for (var i = 1; i < 6; i++) weights[i] = tiny;
        weights[56] = 1; weights[64] = -1;
        // Bias and input 0 cancel before five tiny additions. Reversing the
        // additions or adding the bias last would lose these terms at magnitude 8.
        var expected = Math.Tanh(Math.Tanh(5 * tiny));
        var outputs = Evaluate(new NeuralGenome(weights), [1, 1, 1, 1, 1, 1]);
        AssertBits([expected, -expected], outputs);
    }

    [Fact]
    public void OutputSummationStartsWithBiasAndUsesAscendingHiddenOrder()
    {
        var weights = new double[74]; weights[48] = 8;
        for (var h = 1; h < 8; h++) weights[48 + h] = 0.5;
        weights[72] = 8; weights[56] = -8;
        weights[73] = -8; weights[64] = 8;
        var tiny = Math.ScaleB(1, -52);
        for (var h = 1; h < 8; h++) { weights[56 + h] = tiny; weights[64 + h] = -tiny; }
        // tanh(8) is not exactly 1; preserve the bias-first cancellation residual.
        var sum = 8 - 8 * Math.Tanh(8);
        for (var h = 1; h < 8; h++) sum += tiny * Math.Tanh(0.5);
        AssertBits([Math.Tanh(sum), -Math.Tanh(sum)], Evaluate(new NeuralGenome(weights), new double[6]));
    }

    [Theory]
    [InlineData(0, 2, "inputs")]
    [InlineData(5, 2, "inputs")]
    [InlineData(7, 2, "inputs")]
    [InlineData(6, 0, "outputs")]
    [InlineData(6, 1, "outputs")]
    [InlineData(6, 3, "outputs")]
    public void InvalidSpanLengthsLeaveOutputsUntouched(int inputsCount, int outputsCount, string parameter)
    {
        var outputs = Enumerable.Repeat(42.25, outputsCount).ToArray();
        var exception = Assert.Throws<ArgumentException>(() => NeuralGenome.CreateZero().Forward(new double[inputsCount], outputs));
        Assert.Equal(parameter, exception.ParamName); Assert.All(outputs, o => Assert.Equal(42.25, o));
    }

    public static IEnumerable<object[]> InvalidInputs()
    {
        foreach (var index in Enumerable.Range(0, 6))
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                Math.BitIncrement(1.0), Math.BitDecrement(-1.0), double.MaxValue, -double.MaxValue })
                yield return [index, value];
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void EveryInvalidInputIsRejectedBeforeEitherOutputIsWritten(int index, double value)
    {
        var genome = new NeuralGenome(DenseWeights()); var inputs = new double[6]; inputs[index] = value;
        double[] outputs = [42.25, -13.5];
        var exception = Assert.Throws<ArgumentException>(() => genome.Forward(inputs, outputs));
        Assert.Equal("inputs", exception.ParamName); Assert.Contains($"index {index}", exception.Message);
        Assert.Equal(new[] { 42.25, -13.5 }, outputs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void AllOverlapPositionsMatchNonoverlappingEvaluation(int outputStart)
    {
        var genome = new NeuralGenome(DenseWeights()); var inputs = DenseInputs();
        var expected = Evaluate(genome, inputs); var backing = new double[10]; inputs.CopyTo(backing, 2);
        genome.Forward(backing.AsSpan(2, 6), backing.AsSpan(outputStart, 2));
        AssertBits(expected, backing.AsSpan(outputStart, 2).ToArray());
    }

    [Fact]
    public void InvalidOverlappingInputsDoNotWriteOutputOrBackingArray()
    {
        var genome = NeuralGenome.CreateZero(); double[] backing = [1, 0, 0, 0, 0, double.NaN];
        var expectedBits = backing.Select(BitConverter.DoubleToInt64Bits).ToArray();
        Assert.Throws<ArgumentException>(() => genome.Forward(backing, backing.AsSpan(0, 2)));
        Assert.Equal(expectedBits, backing.Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public void SuccessfulCallsOverwriteBothSlotsAndPreserveNonoverlappingInputs()
    {
        var genome = new NeuralGenome(DenseWeights()); var inputs = DenseInputs(); var before = (double[])inputs.Clone();
        double[] outputs = [double.NaN, double.PositiveInfinity]; genome.Forward(inputs, outputs);
        Near(-0.46687760837527126, outputs[0]); Near(0.47615711535949956, outputs[1]);
        Assert.Equal(before, inputs);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(0)]
    public void BoundWeightPatternsAreFiniteForAll64BoundaryInputVectors(int pattern)
    {
        var weights = Enumerable.Range(0, 74).Select(i => pattern == 0 ? (i % 2 == 0 ? -8.0 : 8.0) : pattern * 8.0).ToArray();
        var genome = new NeuralGenome(weights);
        for (var mask = 0; mask < 64; mask++)
        {
            var inputs = Enumerable.Range(0, 6).Select(i => (mask & (1 << i)) == 0 ? -1.0 : 1.0).ToArray();
            foreach (var output in Evaluate(genome, inputs))
            {
                Assert.True(double.IsFinite(output)); Assert.InRange(output, -1, 1);
            }
        }
    }

    [Fact]
    public void IdenticalInstancesAnd5000RepeatedCallsHaveBitIdenticalOutputs()
    {
        var first = new NeuralGenome(DenseWeights()); var second = new NeuralGenome(DenseWeights());
        var inputs = DenseInputs(); var expected = Evaluate(first, inputs); var weights = first.Weights.ToArray();
        for (var i = 0; i < 5000; i++)
        {
            AssertBits(expected, Evaluate(first, inputs)); AssertBits(expected, Evaluate(second, inputs));
        }
        Assert.Equal(weights, first.Weights.ToArray()); Assert.Equal(weights, second.Weights.ToArray());
    }

    [Fact]
    public void ForwardHasZeroHeapAllocationAfterWarmup()
    {
        var genome = new NeuralGenome(DenseWeights()); var inputs = DenseInputs(); var outputs = new double[2];
        for (var i = 0; i < 100; i++) genome.Forward(inputs, outputs);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 5000; i++) genome.Forward(inputs, outputs);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated); Assert.All(outputs, o => Assert.True(double.IsFinite(o)));
    }

    private static double[] DenseWeights() => Enumerable.Range(0, 74).Select(i => (i % 17 - 8) / 16.0).ToArray();
    private static double[] DenseInputs() => [-1, -0.75, -0.25, 0.125, 0.5, 1];
    private static double[] Evaluate(NeuralGenome genome, double[] inputs)
    {
        double[] outputs = [double.NaN, double.NaN]; genome.Forward(inputs, outputs); return outputs;
    }
    private static void Near(double expected, double actual) => Assert.InRange(Math.Abs(actual - expected), 0, Tolerance);
    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
