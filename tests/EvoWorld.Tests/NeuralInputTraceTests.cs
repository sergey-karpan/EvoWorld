using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class NeuralInputTraceTests
{
    [Fact]
    public void ResolverIdentityOverloadPreservesOriginalValuesTiesAndUnavailableFood()
    {
        var h = PredationFixture.Prey(sensing: 30);
        Food[] food = [new(99, 49, 40, 20), new(7, 51, 40, 30)];
        var consumed = new bool[2];
        Assert.Equal(TargetSensing.Resolve(h, food, consumed, []), TargetSensing.Resolve(h, food, consumed, [], out var id)); Assert.Equal(7, id);
        consumed[1] = true; Assert.Equal(new SensedTarget(TargetKind.Food, 49, 40), TargetSensing.Resolve(h, food, consumed, [], out id)); Assert.Equal(99, id);
        consumed[0] = true; Assert.Equal(default, TargetSensing.Resolve(h, food, consumed, [], out id)); Assert.Null(id);
        var p = PredationFixture.Predator(id: 1, sensing: 30);
        Organism[] prey = [PredationFixture.Prey(id: 99, x: 49), PredationFixture.Prey(id: 7, x: 51), p];
        Assert.Equal(TargetSensing.Resolve(p, [], [], prey), TargetSensing.Resolve(p, [], [], prey, out id)); Assert.Equal(7, id);
        Assert.Equal(default, TargetSensing.Resolve(p, [], [], [p], out id)); Assert.Null(id);
    }

    [Fact]
    public void ExecutingInputsUsePostDrainEnergyAndTheActualEarlierConsumptionMask()
    {
        var first = NeuralFixture.Organism(id: 7, x: 50, y: 40, speed: 0, metabolism: 0);
        var tracked = NeuralFixture.Organism(NeuralFixture.ChannelBrain(3), id: 36, x: 51, y: 40, speed: 0, metabolism: 2, energy: 75, sensing: 30);
        Food[] food = [new(7, 50, 40, 20), new(99, 55, 40, 20)];
        var settings = NeuralFixture.Settings(organisms: 2, food: 2, pickup: 2);
        var simulation = NeuralFixture.Create([first, tracked], food, settings);
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true; simulation.AdvanceTick();
        var sample = simulation.LastResearchSample!;
        Assert.Equal(73, sample.PostDrainEnergy); Assert.Equal(99, sample.TargetId); Assert.Equal(TargetKind.Food, sample.TargetKind);
        var expected = new double[6]; NeuralControl.EncodeInputs(tracked, 73, new SensedTarget(TargetKind.Food, 55, 40), settings, expected);
        var actual = new double[6]; sample.Inputs!.Value.CopyTo(actual); Assert.Equal(expected, actual);
        double[] outputs = new double[2]; tracked.Brain.Forward(expected, outputs);
        Assert.Equal(outputs[0], sample.Outputs!.Value.Output0); Assert.Equal(outputs[1], sample.Outputs.Value.Output1);
        Assert.Equal(sample.Outputs.Value.Output0, simulation.LastDiagnosticSample!.NeuralOutputSteering);
        Assert.False(sample.FoodPickedUp); Assert.Null(sample.FoodId); Assert.Null(sample.FoodEnergy);
    }

    [Fact]
    public void ActualFoodBranchAndReflectionFactsAreCapturedWithoutEnergyInference()
    {
        var tracked = NeuralFixture.Organism(id: 36, x: 99, y: 40, heading: 0, speed: 2, energy: 75, metabolism: 1);
        var simulation = NeuralFixture.Create([tracked], [new Food(123, 99, 40, 20)], NeuralFixture.Settings(food: 1, pickup: 2));
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true; simulation.AdvanceTick();
        var s = simulation.LastResearchSample!;
        Assert.Equal(74, s.PostDrainEnergy); Assert.Equal(94, s.EnergyAfterTick); Assert.True(s.FoodPickedUp);
        Assert.Equal(123, s.FoodId); Assert.Equal(20, s.FoodEnergy); Assert.True(s.ReflectedX); Assert.False(s.ReflectedY);
        Assert.Equal(0, s.CommandedHeading); Assert.Equal(Math.PI, s.MovedHeading); Assert.Equal(99, s.CommittedX);
    }

    [Theory]
    [InlineData("Absent")]
    [InlineData("MetabolicDeath")]
    [InlineData("CapturedBeforeAction")]
    [InlineData("Newborn")]
    public void NonActionsHaveNoFabricatedInputsOutputsOrFeeding(string status)
    {
        Simulation simulation = status switch
        {
            "MetabolicDeath" => PredationFixture.Create([PredationFixture.Prey(id: 36, energy: 1, metabolism: 2)], neural: true),
            "CapturedBeforeAction" => PredationFixture.Create([PredationFixture.Predator(), PredationFixture.Prey(id: 36)], neural: true),
            "Newborn" => PredationFixture.Create([PredationFixture.Prey(id: 35, energy: 200)], neural: true, cap: 2),
            _ => PredationFixture.Create([], neural: true)
        };
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true; simulation.AdvanceTick();
        var s = simulation.LastResearchSample!; Assert.False(s.DidAct); Assert.Equal(status, s.Status);
        Assert.Null(s.Inputs); Assert.Null(s.Outputs); Assert.Null(s.FoodPickedUp); Assert.Null(s.FoodId);
    }

    [Fact]
    public void TrackedPreyCapturedAfterActingRetainsActualCommandsButNoCommittedEnergy()
    {
        var simulation = PredationFixture.Create([PredationFixture.Prey(id: 36), PredationFixture.Predator(id: 43)], neural: true);
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true; simulation.AdvanceTick();
        var s = simulation.LastResearchSample!; Assert.True(s.DidAct); Assert.Equal("ActedCaptured", s.Status);
        Assert.NotNull(s.Inputs); Assert.NotNull(s.Outputs); Assert.False(s.AliveAfterTick); Assert.Null(s.EnergyAfterTick);
        Assert.NotNull(s.MovedX); Assert.Null(s.CommittedX);
    }
}
