namespace EvoWorld.Core;

/// <summary>Read-only observations of a committed population; events are since this instance's starting snapshot.</summary>
public readonly record struct PopulationStatistics
{
    public long TickCount { get; }
    public int OrganismCount { get; }
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
        Speed = TraitStatistics.Calculate(organisms, static o => o.Speed);
        Metabolism = TraitStatistics.Calculate(organisms, static o => o.Metabolism);
        SensingRadius = TraitStatistics.Calculate(organisms, static o => o.SensingRadius);
        SteeringRate = TraitStatistics.Calculate(organisms, static o => o.SteeringRate);
    }
}
