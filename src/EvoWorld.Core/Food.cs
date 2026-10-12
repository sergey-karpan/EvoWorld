namespace EvoWorld.Core;

/// <summary>An immutable initial food item. Identity is its category-local ID.</summary>
public sealed class Food
{
    public int Id { get; }
    public double X { get; }
    public double Y { get; }
    public double Energy { get; }

    internal Food(int id, double x, double y, double energy)
    {
        Id = id;
        X = x;
        Y = y;
        Energy = energy;
    }
}
