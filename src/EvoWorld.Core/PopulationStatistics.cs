namespace EvoWorld.Core;

/// <summary>Read-only observations of a committed population; events are since this instance's starting snapshot.</summary>
public readonly record struct PopulationStatistics
{
    public long TickCount { get; }
    public int OrganismCount { get; }
    public int HerbivoreCount { get; }
    public int PredatorCount { get; }
    public int FoodCount { get; }
    public long TotalBirths { get; }
    public long TotalDeaths { get; }
    public TraitStatistics Speed { get; }
    public TraitStatistics Metabolism { get; }
    public TraitStatistics SensingRadius { get; }
    public TraitStatistics SteeringRate { get; }

    internal PopulationStatistics(SimulationState state, long births, long deaths)
    {
        TickCount = state.TickCount;
        OrganismCount = state.World.Organisms.Count; FoodCount = state.World.FoodItems.Count;
        TotalBirths = births; TotalDeaths = deaths;
        var organisms = state.World.Organisms;
        var herbivores = 0;
        var predators = 0;
        for (var index = 0; index < organisms.Count; index++)
        {
            var organism = organisms[index];
            if (organism.Species == Species.Herbivore) herbivores++;
            else if (organism.Species == Species.Predator) predators++;
            else throw new InvalidOperationException("Undefined organism species.");
        }
        if ((long)herbivores + predators != OrganismCount)
            throw new InvalidOperationException("Species counts must cover the committed population.");
        HerbivoreCount = herbivores;
        PredatorCount = predators;
        Speed = TraitStatistics.Calculate(organisms, static o => o.Speed);
        Metabolism = TraitStatistics.Calculate(organisms, static o => o.Metabolism);
        SensingRadius = TraitStatistics.Calculate(organisms, static o => o.SensingRadius);
        SteeringRate = TraitStatistics.Calculate(organisms, static o => o.SteeringRate);
    }
}
