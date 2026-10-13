namespace EvoWorld.Core;

/// <summary>An immutable initial organism. Identity is its category-local ID.</summary>
public sealed class Organism
{
    public int Id { get; }
    public double X { get; }
    public double Y { get; }
    public double HeadingRadians { get; }
    public double Energy { get; }
    public double Speed { get; }
    public double Metabolism { get; }

    internal Organism(int id, double x, double y, double headingRadians, double energy)
        : this(id, x, y, headingRadians, energy, 50, 1)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism)
    {
        if (!double.IsFinite(speed) || speed < 0) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!double.IsFinite(metabolism) || metabolism < 0) throw new ArgumentOutOfRangeException(nameof(metabolism));
        Id = id;
        X = x;
        Y = y;
        HeadingRadians = headingRadians;
        Energy = energy;
        Speed = speed;
        Metabolism = metabolism;
    }
}
