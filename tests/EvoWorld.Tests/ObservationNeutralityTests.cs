using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class ObservationNeutralityTests
{
    [Fact]
    public void FullLifespanReferenceAndInstrumentedReplayMatchEveryCommittedSnapshot()
    {
        var result = EnergyResearch.Replay.Value;
        Assert.InRange(result.BirthTick, 2022, 2032); Assert.Equal(result.EndTick + 1, result.ComparedSnapshots);
        Assert.True(result.LastAliveTick > result.BirthTick); Assert.True(result.ReplaySeconds < 1800);
        Assert.Equal(result.EndTick - result.BirthTick + 1, result.Rows.Count);
        Assert.Equal("Newborn", result.Rows[0].Sample.Status); Assert.False(result.Rows[^1].Sample.AliveAfterTick);
        var brain = new NeuralGenome(result.Weights); var inputs = new double[6]; var outputs = new double[2];
        Assert.All(result.Rows.Where(r => r.Sample.DidAct), r =>
        {
            Assert.Equal(36, r.Sample.OrganismId); Assert.Equal(9, r.Sample.ParentId);
            Assert.NotNull(r.Sample.Inputs); Assert.NotNull(r.Sample.Outputs);
            Assert.Equal(2 * Math.Min(r.Sample.PostDrainEnergy!.Value / 100, 1) - 1, r.Sample.Inputs!.Value.Input3);
            r.Sample.Inputs.Value.CopyTo(inputs); brain.Forward(inputs, outputs); // Offline verification only.
            Assert.Equal(BitConverter.DoubleToInt64Bits(outputs[0]), BitConverter.DoubleToInt64Bits(r.Sample.Outputs!.Value.Output0));
            Assert.Equal(BitConverter.DoubleToInt64Bits(outputs[1]), BitConverter.DoubleToInt64Bits(r.Sample.Outputs.Value.Output1));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedTickKeepsThePreviousCommittedResearchSampleAndRetriesExactly(int failure)
    {
        Simulation Create()
        {
            var s = PredationFixture.Create([PredationFixture.Prey(id: 36, energy: 300)], neural: true, mutation: 0.1, cap: 4, radius: 0);
            s.EnableDiagnosticsFor(36); s.CaptureResearchTrace = true; s.AdvanceTick(); return s;
        }
        var simulation = Create(); var clean = Create(); var sample = simulation.LastResearchSample; var state = simulation.State;
        var random = simulation.RandomState; var nextId = simulation.NextOrganismId; var foodId = simulation.NextFoodId;
        simulation.FailBeforeMutationForTesting = failure == 0;
        simulation.FailWorldConstructionForTesting = failure == 1;
        simulation.FailOffspringTraitConstructionForTesting = failure == 2;
        Assert.ThrowsAny<Exception>(simulation.AdvanceTick);
        Assert.Same(sample, simulation.LastResearchSample); Assert.Same(state, simulation.State);
        Assert.Equal(random, simulation.RandomState); Assert.Equal(nextId, simulation.NextOrganismId); Assert.Equal(foodId, simulation.NextFoodId);
        simulation.FailBeforeMutationForTesting = simulation.FailWorldConstructionForTesting = simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick(); PredationFixture.Same(clean, simulation);
        Assert.Equal(clean.LastResearchSample, simulation.LastResearchSample); Assert.NotSame(sample, simulation.LastResearchSample);
    }

    [Fact]
    public void ResearchIsOptInAndToggleOrRetargetCannotExposeStaleSignals()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 36)], neural: true);
        Assert.False(simulation.CaptureResearchTrace); simulation.EnableDiagnosticsFor(36); simulation.AdvanceTick(); Assert.Null(simulation.LastResearchSample);
        simulation.CaptureResearchTrace = true; Assert.Null(simulation.LastResearchSample); simulation.AdvanceTick();
        var old = simulation.LastResearchSample; Assert.NotNull(old);
        simulation.CaptureResearchTrace = false; Assert.Null(simulation.LastResearchSample);
        simulation.CaptureResearchTrace = true; Assert.Null(simulation.LastResearchSample); simulation.AdvanceTick(); Assert.NotSame(old, simulation.LastResearchSample);
        simulation.EnableDiagnosticsFor(99); Assert.Null(simulation.LastResearchSample); simulation.AdvanceTick();
        Assert.Equal("Absent", simulation.LastResearchSample!.Status); Assert.Null(simulation.LastResearchSample.Inputs);
        simulation.DisableDiagnostics(); Assert.Null(simulation.LastResearchSample);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MidRunEnableDisableDoesNotChangePublicCommandsWorldRngIdsOrParentage(bool neural)
    {
        var reference = PredationFixture.Create([PredationFixture.Prey(id: 36, energy: 300)], neural: neural, cap: 4, radius: 0, mutation: 0.1);
        var observed = PredationFixture.Create([PredationFixture.Prey(id: 36, energy: 300)], neural: neural, cap: 4, radius: 0, mutation: 0.1);
        reference.EnableDiagnosticsFor(36); observed.EnableDiagnosticsFor(36);
        for (var tick = 1; tick <= 100; tick++)
        {
            observed.CaptureResearchTrace = tick % 3 == 0;
            reference.AdvanceTick(); observed.AdvanceTick(); PredationFixture.Same(reference, observed);
            Assert.Equal(reference.State.World.Organisms.Select(o => o.ParentId), observed.State.World.Organisms.Select(o => o.ParentId));
            if (!observed.CaptureResearchTrace) Assert.Null(observed.LastResearchSample);
        }
    }
}
