using EvoWorld.Core.Random;

namespace EvoWorld.Core;

public static class WorldInitializer
{
    internal const double InitialOrganismEnergy = 100.0;
    internal const double InitialFoodEnergy = 20.0;

    /// <summary>
    /// Creates herbivores then predators, consuming X, Y, heading per ascending ID,
    /// then food, consuming X, Y per ascending ID. No placement retries.
    /// </summary>
    public static WorldState Create(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Create(settings, new Pcg32(settings.Seed));
    }

    internal static WorldState Create(SimulationSettings settings, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var total = (long)settings.InitialOrganismCount + settings.InitialPredatorCount;
        if (total > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(settings), "Combined initial population exceeds the supported array length.");
        var organisms = new Organism[(int)total];
        var foodItems = new Food[settings.InitialFoodCount];

        for (var id = 0; id < organisms.Length; id++)
        {
            var x = NextScaled(random, settings.WorldWidth);
            var y = NextScaled(random, settings.WorldHeight);
            var heading = NextScaled(random, 2.0 * Math.PI);
            var species = id < settings.InitialOrganismCount ? Species.Herbivore : Species.Predator;
            var energy = species == Species.Herbivore ? InitialOrganismEnergy : settings.InitialPredatorEnergy;
            organisms[id] = new Organism(id, x, y, heading, energy,
                settings.OrganismSpeed, settings.OrganismEnergyDrainPerSecond,
                settings.OrganismSensingRadius, settings.OrganismSteeringRate, Organism.ZeroBrain, species);
        }

        for (var id = 0; id < foodItems.Length; id++)
        {
            var x = NextScaled(random, settings.WorldWidth);
            var y = NextScaled(random, settings.WorldHeight);
            foodItems[id] = new Food(id, x, y, InitialFoodEnergy);
        }

        if (settings.InitialSpeedVariationAmplitude != 0 || settings.InitialMetabolismVariationAmplitude != 0)
        {
            for (var id = 0; id < organisms.Length; id++)
            {
                var organism = organisms[id];
                var speed = TraitVariation.Apply(organism.Speed, settings.InitialSpeedVariationAmplitude,
                    settings.MinSpeed, settings.MaxSpeed, random.NextUInt());
                var metabolism = TraitVariation.Apply(organism.Metabolism, settings.InitialMetabolismVariationAmplitude,
                    settings.MinMetabolism, settings.MaxMetabolism, random.NextUInt());
                organisms[id] = new Organism(id, organism.X, organism.Y, organism.HeadingRadians,
                    organism.Energy, speed, metabolism, organism.SensingRadius, organism.SteeringRate, organism.Brain, organism.Species);
            }
        }
        if (settings.InitialSensingRadiusVariationAmplitude != 0 || settings.InitialSteeringRateVariationAmplitude != 0)
        {
            for (var id = 0; id < organisms.Length; id++)
            {
                var organism = organisms[id];
                var sensing = TraitVariation.Apply(organism.SensingRadius, settings.InitialSensingRadiusVariationAmplitude,
                    settings.MinSensingRadius, settings.MaxSensingRadius, random.NextUInt());
                var steering = TraitVariation.Apply(organism.SteeringRate, settings.InitialSteeringRateVariationAmplitude,
                    settings.MinSteeringRate, settings.MaxSteeringRate, random.NextUInt());
                organisms[id] = new Organism(id, organism.X, organism.Y, organism.HeadingRadians,
                    organism.Energy, organism.Speed, organism.Metabolism, sensing, steering, organism.Brain, organism.Species);
            }
        }
        if (settings.EnableNeuralControl && settings.InitialNeuralWeightVariationAmplitude > 0)
        {
            // Complete placement and both trait phases first; fixed organism/parameter order.
            var weights = new double[NeuralGenome.WeightCount];
            for (var id = 0; id < organisms.Length; id++)
            {
                for (var index = 0; index < weights.Length; index++)
                {
                    var signed = 2.0 * (random.NextUInt() / 4294967296.0) - 1.0;
                    weights[index] = Math.Clamp(signed * settings.InitialNeuralWeightVariationAmplitude,
                        settings.NeuralWeightMin, settings.NeuralWeightMax);
                }
                var organism = organisms[id];
                organisms[id] = new Organism(id, organism.X, organism.Y, organism.HeadingRadians,
                    organism.Energy, organism.Speed, organism.Metabolism, organism.SensingRadius, organism.SteeringRate,
                    new NeuralGenome(weights), organism.Species);
            }
        }
        return new WorldState(settings.WorldWidth, settings.WorldHeight, organisms, foodItems);
    }

    internal static double NextScaled(Pcg32 random, double upperBound)
    {
        var value = (random.NextUInt() / 4294967296.0) * upperBound;
        // Subnormal dimensions can round a product up to the exclusive bound.
        return value < upperBound ? value : Math.BitDecrement(upperBound);
    }
}
