namespace EvoWorld.Core.Random;

/// <summary>A complete immutable snapshot of a PCG32 generator.</summary>
public readonly record struct Pcg32State(ulong State, ulong Increment);
