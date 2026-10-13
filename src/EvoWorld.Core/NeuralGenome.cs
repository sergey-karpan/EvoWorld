namespace EvoWorld.Core;

/// <summary>An immutable, fixed 6-to-8-to-2 tanh network, independent of the simulation.</summary>
/// <remarks>
/// Parameters are a privately owned defensive copy in row-major order:
/// indices 0..47: input-to-hidden weight [h,i] at h*6+i;
/// indices 48..55: hidden bias [h] at 48+h;
/// indices 56..71: hidden-to-output weight [o,h] at 56+o*8+h;
/// indices 72..73: output bias [o] at 72+o.
/// Inputs are normalized to [-1,1], and weights and biases are bounded by 8.
/// Repeated evaluation is bit-identical on the same supported runtime; ordinary
/// floating-point and Math.Tanh behavior need not be bit-identical across platforms.
/// Input and output meanings, initialization, and mutation belong to later tasks.
/// </remarks>
public sealed class NeuralGenome
{
    public const int InputCount = 6;
    public const int HiddenCount = 8;
    public const int OutputCount = 2;
    public const double MaxAbsoluteWeight = 8.0;
    public const int WeightCount = 74;

    private const int HiddenBiasOffset = 48;
    private const int HiddenOutputOffset = 56;
    private const int OutputBiasOffset = 72;
    private readonly double[] _weights;

    /// <summary>A read-only view of the 74 owned parameters in the documented order.</summary>
    public ReadOnlySpan<double> Weights => _weights;

    /// <summary>Validates and defensively copies exactly 74 finite parameters in [-8,8].</summary>
    public NeuralGenome(ReadOnlySpan<double> weights)
    {
        if (weights.Length != WeightCount)
            throw new ArgumentException($"Exactly {WeightCount} weights and biases are required.", nameof(weights));
        for (var i = 0; i < weights.Length; i++)
            if (!double.IsFinite(weights[i]) || weights[i] < -MaxAbsoluteWeight || weights[i] > MaxAbsoluteWeight)
                throw new ArgumentException($"Parameter at index {i} must be finite and in [-8,8].", nameof(weights));
        _weights = weights.ToArray();
    }

    /// <summary>Creates 74 zero parameters; both outputs are exactly zero for every valid input.</summary>
    public static NeuralGenome CreateZero()
    {
        Span<double> weights = stackalloc double[WeightCount];
        weights.Clear();
        return new NeuralGenome(weights);
    }

    /// <summary>Evaluates six finite inputs in [-1,1], writing exactly two tanh outputs in [-1,1].</summary>
    /// <remarks>
    /// Validates both lengths and every input before writing any output. Overlap
    /// is supported: all inputs are read into the complete hidden layer before
    /// either output is written. Neurons and terms are evaluated in ascending
    /// index order, starting with the bias and using separate multiply and add.
    /// Hidden pre-activations are bounded by 56, output pre-activations by 72.
    /// Uses only stack scratch space and never changes parameters or consumes RNG.
    /// </remarks>
    public void Forward(ReadOnlySpan<double> inputs, Span<double> outputs)
    {
        if (inputs.Length != InputCount)
            throw new ArgumentException($"Exactly {InputCount} inputs are required.", nameof(inputs));
        if (outputs.Length != OutputCount)
            throw new ArgumentException($"Exactly {OutputCount} output slots are required.", nameof(outputs));
        for (var i = 0; i < inputs.Length; i++)
            if (!double.IsFinite(inputs[i]) || inputs[i] < -1 || inputs[i] > 1)
                throw new ArgumentException($"Input at index {i} must be finite and in [-1,1].", nameof(inputs));

        Span<double> hidden = stackalloc double[HiddenCount];
        for (var h = 0; h < HiddenCount; h++)
        {
            var sum = _weights[HiddenBiasOffset + h];
            for (var i = 0; i < InputCount; i++)
            {
                var product = _weights[h * InputCount + i] * inputs[i];
                sum = sum + product;
            }
            hidden[h] = Math.Tanh(sum);
        }
        for (var o = 0; o < OutputCount; o++)
        {
            var sum = _weights[OutputBiasOffset + o];
            for (var h = 0; h < HiddenCount; h++)
            {
                var product = _weights[HiddenOutputOffset + o * HiddenCount + h] * hidden[h];
                sum = sum + product;
            }
            outputs[o] = Math.Tanh(sum);
        }
    }
}
