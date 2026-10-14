using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Tests.Fixtures;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PredatorBehaviorDeterminismTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedSpeciesReproduceCompleteStateAndTargetTelemetryForFiveThousandTicks(bool neural)
    {
        var settings = PredatorFixture.Settings(herbivores: 3, predators: 2, food: 10, energy: 200,
            neural: neural, oldVariation: true, newVariation: true, foodCap: 40);
        var first = new Simulation(settings); var second = new Simulation(settings);
        first.EnableDiagnosticsFor(3); second.EnableDiagnosticsFor(3);
        for (var tick = 0; tick <= 5000; tick++)
        {
            PredatorFixture.SameState(first, second); PredatorFixture.SameSample(first.LastDiagnosticSample, second.LastDiagnosticSample);
            if (tick > 0) { Assert.Equal(tick, first.LastDiagnosticSample!.TickCount); Assert.Equal(3, first.LastDiagnosticSample.OrganismId); }
            if (tick < 5000) { first.AdvanceTick(); second.AdvanceTick(); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsEnabledAndDisabledRunsHaveIdenticalWorldsRngIdsAndStatistics(bool neural)
    {
        var settings = PredatorFixture.Settings(herbivores: 2, predators: 2, neural: neural, food: 10,
            oldVariation: true, newVariation: true, foodCap: 30);
        var enabled = new Simulation(settings); var disabled = new Simulation(settings); enabled.EnableDiagnosticsFor(2);
        for (var tick = 1; tick <= 600; tick++)
        {
            enabled.AdvanceTick(); disabled.AdvanceTick(); PredatorFixture.SameState(enabled, disabled);
            Assert.Null(disabled.LastDiagnosticSample); Assert.Equal(tick, enabled.LastDiagnosticSample!.TickCount);
        }
    }

    [Theory]
    [InlineData(false, 0.0, 6)]
    [InlineData(false, 0.125, 6)]
    [InlineData(true, 0.0, 6)]
    [InlineData(true, 0.125, 80)]
    public void FoodRegenerationStillPrecedesSixOrEightyDrawBirths(bool neural, double mutation, int draws)
    {
        var settings = PredatorFixture.Settings(neural: neural, mutation: mutation, food: 0, duration: 0.2);
        var herbivore = PredatorFixture.Organism(Species.Herbivore, id: 7, energy: 500);
        var predator = PredatorFixture.Organism(Species.Predator, id: 43, energy: 500);
        var simulation = PredatorFixture.Create(settings, herbivore, predator);
        var before = simulation.RandomState; var reference = new Pcg32(before);
        var x = reference.NextUInt() / 4294967296.0 * 100;
        var y = reference.NextUInt() / 4294967296.0 * 80;
        simulation.EnableDiagnosticsFor(43); simulation.AdvanceTick();
        var food = Assert.Single(simulation.State.World.FoodItems); Assert.Equal((x, y), (food.X, food.Y));
        Assert.Equal(2, simulation.GetStatistics().TotalBirths); Assert.Equal(TargetKind.Prey, simulation.LastDiagnosticSample!.TargetKind);
        for (var parent = 0; parent < 2; parent++)
        {
            for (var i = 0; i < 6; i++) reference.NextUInt();
            var brain = parent == 0 ? herbivore.Brain : predator.Brain;
            if (neural && mutation > 0)
                NeuralMutationFixture.AssertBits(NeuralMutationFixture.MutatedWeights(brain, mutation, -1, 1, reference), simulation.State.World.Organisms[parent + 2].Brain);
            else Assert.Same(brain, simulation.State.World.Organisms[parent + 2].Brain);
        }
        Assert.Equal(reference.CaptureState(), simulation.RandomState);
        var count = new Pcg32(before); for (var i = 0; i < 2 + 2 * draws; i++) count.NextUInt();
        Assert.Equal(count.CaptureState(), simulation.RandomState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoPredatorRunMatchesIndependentTask12aClosedFormWorldRngHistoryAndSamples(bool neural)
    {
        var reference = new HerbivorePursuitReference();
        var settings = NeuralFixture.Settings(enabled: neural, organisms: 2, food: 2, duration: 1,
            amplitude: 0, baselineSpeed: 0, pickup: 0, cap: 3, speedMutation: 0, metabolismMutation: 0,
            sensingMutation: 0, steeringMutation: 0);
        var initial = reference.InitialWorld(); var simulation = new Simulation(settings, initial);
        simulation.EnableDiagnosticsFor(43);
        for (var tick = 1; tick <= 40; tick++)
        {
            simulation.AdvanceTick();
            var expectedWorld = reference.WorldAt(tick); var actual = simulation.State.World;
            Assert.Equal(expectedWorld.WorldWidth, actual.WorldWidth); Assert.Equal(expectedWorld.WorldHeight, actual.WorldHeight);
            Assert.Equal(expectedWorld.Organisms.Select(o => (o.Id, o.Species)), actual.Organisms.Select(o => (o.Id, o.Species)));
            for (var i = 0; i < expectedWorld.Organisms.Count; i++)
            {
                var e = expectedWorld.Organisms[i]; var a = actual.Organisms[i];
                double[] expected = [e.X, e.Y, e.HeadingRadians, e.Energy, e.Speed, e.Metabolism, e.SensingRadius, e.SteeringRate];
                double[] observed = [a.X, a.Y, a.HeadingRadians, a.Energy, a.Speed, a.Metabolism, a.SensingRadius, a.SteeringRate];
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), observed.Select(BitConverter.DoubleToInt64Bits));
                NeuralMutationFixture.AssertBits(e.Brain.Weights.ToArray(), a.Brain);
            }
            Assert.Empty(actual.FoodItems); Assert.Equal(reference.FinalRandom, simulation.RandomState);
            Assert.Equal(18, simulation.NextFoodId); Assert.Equal(45, simulation.NextOrganismId);
            var statistics = new PopulationStatistics(new SimulationState(expectedWorld, tick), 1, tick >= 26 ? 1 : 0);
            Assert.Equal(statistics, simulation.GetStatistics());
            var expectedSample = new OrganismDiagnosticSample(43, tick, true, neural,
                targetKind: tick == 1 ? TargetKind.Food : TargetKind.None);
            PredatorFixture.SameSample(expectedSample, simulation.LastDiagnosticSample);
        }
        Assert.Equal(100, initial.Organisms[0].Energy); Assert.Equal(160, initial.Organisms[1].Energy); Assert.Equal(2, initial.FoodItems.Count);
    }
}
