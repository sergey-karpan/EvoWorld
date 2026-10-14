using EvoWorld.Core;
using EvoWorld.Core.Random;

namespace EvoWorld.Tests.Fixtures;

// Independent closed-form Task 12a scenario; no controller, target helper,
// Simulation.AdvanceTick, or variation implementation is used by this oracle.
// Seed42, H=2, F=2, no initial variations => 10 placement draws.
// Stationary herbivores: #7 (50,40), E100, metabolism1; #43 (70,40),
// E160, metabolism2. Each eats its coincident E20 food in tick1.
// #43 alone reproduces: E178 -> E128, child #44 E50/metabolism2.
// One birth draws position, heading, then four traits, even with amplitudes0.
// No regeneration or further births; #44 dies in tick26. dt=1, speeds/rates0.
// Therefore E7(t)=120-t, E43(t)=130-2t, E44(t)=50-2(t-1) while positive.
// Child offsets stay strictly inside the world, so no clamps/reflections apply.
// The PCG primitive is already verified against historical independent goldens;
// its draw schedule and every subsequent world value here are scalar oracles.
internal sealed class HerbivorePursuitReference
{
    private readonly NeuralGenome _brain = Organism.ZeroBrain;
    private readonly double _childX;
    private readonly double _childY;
    private readonly double _childHeading;
    internal Pcg32State FinalRandom { get; }

    internal HerbivorePursuitReference()
    {
        var reference = new Pcg32(42);
        for (var i = 0; i < 10; i++) reference.NextUInt();
        var angle = (reference.NextUInt() / 4294967296.0) * (2 * Math.PI);
        _childX = 70 + 5 * Math.Cos(angle);
        _childY = 40 + 5 * Math.Sin(angle);
        _childHeading = Math.PI + (2 * (reference.NextUInt() / 4294967296.0) - 1) * 0.1;
        for (var i = 0; i < 4; i++) reference.NextUInt();
        FinalRandom = reference.CaptureState();
    }

    internal WorldState InitialWorld() => new(100, 80,
        [new Organism(7, 50, 40, 0, 100, 0, 1, 30, 0, _brain),
         new Organism(43, 70, 40, Math.PI, 160, 0, 2, 30, 0, _brain)],
        [new Food(9, 50, 40, 20), new Food(17, 70, 40, 20)]);

    internal WorldState WorldAt(int tick)
    {
        var organisms = new List<Organism>
        {
            new(7, 50, 40, 0, 120 - tick, 0, 1, 30, 0, _brain),
            new(43, 70, 40, Math.PI, 130 - 2 * tick, 0, 2, 30, 0, _brain)
        };
        if (tick < 26) organisms.Add(new(44, _childX, _childY, _childHeading, 50 - 2 * (tick - 1), 0, 2, 30, 0, _brain));
        return new(100, 80, organisms, []);
    }
}
