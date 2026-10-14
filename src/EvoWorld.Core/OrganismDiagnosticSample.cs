namespace EvoWorld.Core;

/// <summary>Immutable observations of one organism in a successfully committed tick.
/// Commands precede movement and reflection; absent/newborn/dead organisms did not act.</summary>
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

    internal OrganismDiagnosticSample(int organismId, long tickCount, bool didAct, bool neural,
        double steering = 0, double speed = 0, double rotation = 0, double effectiveSpeed = 0,
        TargetKind targetKind = TargetKind.None)
    {
        OrganismId = organismId;
        TickCount = tickCount;
        DidAct = didAct;
        IsNeuralMode = neural;
        TargetKind = didAct ? targetKind : TargetKind.None;
        if (!didAct || !neural) return;
        NeuralOutputSteering = steering;
        NeuralOutputSpeed = speed;
        AppliedRotationRadians = rotation;
        EffectiveSpeed = effectiveSpeed;
    }
}
