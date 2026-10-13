namespace EvoWorld.Core;

/// <summary>An immutable initial organism. Identity is its category-local ID.</summary>
public sealed class Organism
{
    private static readonly NeuralGenome ZeroBrain = NeuralGenome.CreateZero();
    public int Id { get; }
    public double X { get; }
    public double Y { get; }
    public double HeadingRadians { get; }
    public double Energy { get; }
    public double Speed { get; }
    public double Metabolism { get; }
    public double SensingRadius { get; }
    public double SteeringRate { get; }
    public NeuralGenome Brain { get; }

    internal Organism(int id, double x, double y, double headingRadians, double energy)
        : this(id, x, y, headingRadians, energy, 50, 1)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism)
        : this(id, x, y, headingRadians, energy, speed, metabolism, 0, 0)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism,
        double sensingRadius, double steeringRate)
        : this(id, x, y, headingRadians, energy, speed, metabolism, sensingRadius, steeringRate, ZeroBrain)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism,
        double sensingRadius, double steeringRate, NeuralGenome brain)
    {
        ArgumentNullException.ThrowIfNull(brain);
        if (!double.IsFinite(speed) || speed < 0) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!double.IsFinite(metabolism) || metabolism < 0) throw new ArgumentOutOfRangeException(nameof(metabolism));
        if (!double.IsFinite(sensingRadius) || sensingRadius < 0) throw new ArgumentOutOfRangeException(nameof(sensingRadius));
        if (!double.IsFinite(steeringRate) || steeringRate < 0) throw new ArgumentOutOfRangeException(nameof(steeringRate));
        Id = id;
        X = x;
        Y = y;
        HeadingRadians = headingRadians;
        Energy = energy;
        Speed = speed;
        Metabolism = metabolism;
        SensingRadius = sensingRadius;
        SteeringRate = steeringRate;
        Brain = brain;
    }
}
