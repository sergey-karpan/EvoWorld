namespace EvoWorld.Core;

/// <summary>An immutable world and tick-count snapshot.</summary>
public sealed class SimulationState
{
    public WorldState World { get; }
    public long TickCount { get; }

    public SimulationState(WorldState world, long tickCount)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(tickCount);
        World = world;
        TickCount = tickCount;
    }
}
