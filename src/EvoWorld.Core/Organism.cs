namespace EvoWorld.Core;

/// <summary>An immutable organism. Both species share the organism ID sequence.</summary>
public sealed class Organism
{
    internal static readonly NeuralGenome ZeroBrain = NeuralGenome.CreateZero();
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
    public Species Species { get; }
    public int? ParentId { get; }
    public double AttackRadius { get; }
    public double AttackEfficiency { get; }

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
        : this(id, x, y, headingRadians, energy, speed, metabolism, sensingRadius, steeringRate, brain, Species.Herbivore)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism,
        double sensingRadius, double steeringRate, NeuralGenome brain, Species species)
        : this(id, x, y, headingRadians, energy, speed, metabolism, sensingRadius, steeringRate, brain, species, null)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism,
        double sensingRadius, double steeringRate, NeuralGenome brain, Species species, int? parentId)
        : this(id, x, y, headingRadians, energy, speed, metabolism, sensingRadius, steeringRate, brain, species, parentId, 8.0, 1.0)
    {
    }

    internal Organism(int id, double x, double y, double headingRadians, double energy, double speed, double metabolism,
        double sensingRadius, double steeringRate, NeuralGenome brain, Species species, int? parentId,
        double attackRadius, double attackEfficiency)
    {
        if (!double.IsFinite(attackRadius) || attackRadius < 0) throw new ArgumentOutOfRangeException(nameof(attackRadius));
        if (!double.IsFinite(attackEfficiency) || attackEfficiency < 0 || attackEfficiency > 1)
            throw new ArgumentOutOfRangeException(nameof(attackEfficiency));
        if (parentId is < 0 || (parentId.HasValue && parentId.Value == id))
            throw new ArgumentOutOfRangeException(nameof(parentId));
        if (species is not Species.Herbivore and not Species.Predator)
            throw new ArgumentOutOfRangeException(nameof(species));
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
        Species = species;
        ParentId = parentId;
        AttackRadius = attackRadius;
        AttackEfficiency = attackEfficiency;
    }
}
