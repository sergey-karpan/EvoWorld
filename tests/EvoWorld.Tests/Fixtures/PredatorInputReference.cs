namespace EvoWorld.Tests.Fixtures;

// Independent scalar fixture, calculated directly in C#; no external tool.
// World 100x80. Predator (20,20), heading 0, speed 10, energy 60,
// metabolism 2, dt .5, sensing 10, steering 2. Prey (23,24).
// dx=3, dy=4 => distance=5, sin=4/5, cos=3/5, proximity=1-2*5/10=0.
// Post-drain energy=59 => 2*59/100-1=.18; speed 10/200 => -.9.
// Nearest wall=20 >= sensing 10 => -1. Indices are exactly 0..5.
// Six independent hidden neurons h[i]=tanh(input[i]); the two output rows
// below, with biases .1 and -.2, give the analytical output oracle.
internal static class PredatorInputReference
{
    internal static readonly double[] Inputs = [0.8, 0.6, 0, 0.18, -0.9, -1];
    private static readonly double[][] Coefficients =
        [[0.25, 0.125, 0.5, 0.25, 0.125, 0.0625], [-0.125, 0.0625, 0.25, 0.5, -0.25, 0.125]];

    internal static double[] Weights()
    {
        var weights = new double[74];
        for (var i = 0; i < 6; i++) weights[i * 6 + i] = 1;
        for (var output = 0; output < 2; output++)
            for (var i = 0; i < 6; i++) weights[56 + output * 8 + i] = Coefficients[output][i];
        weights[72] = 0.1; weights[73] = -0.2;
        return weights;
    }

    internal static double[] Outputs()
    {
        var result = new double[2];
        for (var output = 0; output < 2; output++)
        {
            var sum = output == 0 ? 0.1 : -0.2;
            for (var i = 0; i < 6; i++) sum = sum + Coefficients[output][i] * Math.Tanh(Inputs[i]);
            result[output] = Math.Tanh(sum);
        }
        return result;
    }
}
