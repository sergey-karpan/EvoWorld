namespace EvoWorld.Core;

/// <summary>Immutable observations of one organism in a successfully committed tick.
/// Commands precede movement and reflection. DidAct records its own action,
/// including prey captured after acting; absent, newborn, or pre-action deaths did not act.</summary>
public sealed class OrganismDiagnosticSample
{
    public int OrganismId { get; }
    public long TickCount { get; }
    public bool DidAct { get; }
    public bool IsNeuralMode { get; }
    public bool IsRuleBasedMode => !IsNeuralMode;
    public double? NeuralOutputSteering { get; }
    public double? NeuralOutputSpeed { get; }
    public double? AppliedRotationRadians { get; }
    public double? EffectiveSpeed { get; }
    public TargetKind TargetKind { get; }
    public int? PreyConsumedId { get; }
    public double? EnergyGainedFromPrey { get; }

    internal OrganismDiagnosticSample(int organismId, long tickCount, bool didAct, bool neural,
        double steering = 0, double speed = 0, double rotation = 0, double effectiveSpeed = 0,
        TargetKind targetKind = TargetKind.None, int? preyConsumedId = null, double? energyGainedFromPrey = null)
    {
        OrganismId = organismId;
        TickCount = tickCount;
        DidAct = didAct;
        IsNeuralMode = neural;
        TargetKind = didAct ? targetKind : TargetKind.None;
        if (didAct && preyConsumedId is not null)
        {
            PreyConsumedId = preyConsumedId;
            EnergyGainedFromPrey = energyGainedFromPrey;
        }
        if (!didAct || !neural) return;
        NeuralOutputSteering = steering;
        NeuralOutputSpeed = speed;
        AppliedRotationRadians = rotation;
        EffectiveSpeed = effectiveSpeed;
    }
}
