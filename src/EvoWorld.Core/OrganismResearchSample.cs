namespace EvoWorld.Core;

// Research-only scalar copies of the executing buffers, never a second evaluation.
internal readonly record struct ResearchInputs(double Input0, double Input1, double Input2, double Input3, double Input4, double Input5)
{
    internal void CopyTo(Span<double> destination)
    {
        destination[0] = Input0; destination[1] = Input1; destination[2] = Input2;
        destination[3] = Input3; destination[4] = Input4; destination[5] = Input5;
    }
}
internal readonly record struct ResearchOutputs(double Output0, double Output1);

// Only the latest committed sample is retained by Core. Nulls mean unavailable,
// including no neural action; they must not be interpreted as zero commands.
internal sealed record OrganismResearchSample(int OrganismId, long TickCount, bool DidAct)
{
    internal string Status { get; init; } = "Absent";
    internal int? ParentId { get; init; }
    internal double? BeforeX { get; init; }
    internal double? BeforeY { get; init; }
    internal double? BeforeHeading { get; init; }
    internal double? EnergyBeforeDrain { get; init; }
    internal double? PostDrainEnergy { get; init; }
    internal ResearchInputs? Inputs { get; init; }
    internal ResearchOutputs? Outputs { get; init; }
    internal TargetKind? TargetKind { get; init; }
    internal int? TargetId { get; init; }
    internal double? TargetX { get; init; }
    internal double? TargetY { get; init; }
    internal double? AppliedRotation { get; init; }
    internal double? EffectiveSpeed { get; init; }
    internal double? SteeringRate { get; init; }
    internal double? InheritedSpeed { get; init; }
    internal double? CommandedHeading { get; init; }
    internal double? MovedX { get; init; }
    internal double? MovedY { get; init; }
    internal double? MovedHeading { get; init; }
    internal bool? ReflectedX { get; init; }
    internal bool? ReflectedY { get; init; }
    internal bool? FoodPickedUp { get; init; }
    internal int? FoodId { get; init; }
    internal double? FoodEnergy { get; init; }
    internal bool AliveAfterTick { get; init; }
    internal double? CommittedX { get; init; }
    internal double? CommittedY { get; init; }
    internal double? CommittedHeading { get; init; }
    internal double? EnergyAfterTick { get; init; }
    internal int BirthsThisTick { get; init; }
}
