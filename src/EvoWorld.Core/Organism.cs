namespace EvoWorld.Core;

/// <summary>An immutable initial organism. Identity is its category-local ID.</summary>
public sealed class Organism
{
    public int Id { get; }
    public double X { get; }
    public double Y { get; }
    public double HeadingRadians { get; }
    public double Energy { get; }

    internal Organism(int id, double x, double y, double headingRadians, double energy)
    {
        Id = id;
        X = x;
        Y = y;
        HeadingRadians = headingRadians;
        Energy = energy;
    }
}
