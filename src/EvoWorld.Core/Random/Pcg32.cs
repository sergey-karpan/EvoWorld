namespace EvoWorld.Core.Random;

/// <summary>PCG-XSH-RR 64/32 setseq with fixed stream selector 1. Not thread-safe.</summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong StreamSelector = 1UL;
    private ulong _state;
    private ulong _increment;

    public Pcg32(ulong seed)
    {
        unchecked
        {
            _state = 0;
            _increment = (StreamSelector << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }
    }

    public uint NextUInt()
    {
        unchecked
        {
            var previousState = _state;
            _state = previousState * Multiplier + _increment;
            var xorshifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
            var rotation = (int)(previousState >> 59);
            return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
        }
    }

    // Temporary transaction copy of the same stream, without seeding or draws.
    internal Pcg32(Pcg32State state) => RestoreState(state);

    public Pcg32State CaptureState() => new(_state, _increment);

    public void RestoreState(Pcg32State state)
    {
        if (state.Increment != 3UL)
            throw new ArgumentException("The fixed PCG32 stream requires increment 3.", nameof(state));

        _state = state.State;
        _increment = state.Increment;
    }
}
