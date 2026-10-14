using EvoWorld.Core;
using Xunit;

namespace EvoWorld.Tests;

public sealed class OrganismDiagnosticsTests
{
    private static void NullCommands(OrganismDiagnosticSample sample)
    {
        Assert.Null(sample.NeuralOutputSteering);
        Assert.Null(sample.NeuralOutputSpeed);
        Assert.Null(sample.AppliedRotationRadians);
        Assert.Null(sample.EffectiveSpeed);
    }

    [Fact]
    public void ObserverConfigurationIsIdempotentAndDoesNotChangeTheTransaction()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(id: 17)]);
        var initial = simulation.State; var random = simulation.RandomState;
        Assert.Null(simulation.LastDiagnosticSample);
        Assert.Null(simulation.DiagnosticOrganismId);
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.EnableDiagnosticsFor(-1));
        simulation.EnableDiagnosticsFor(17);
        simulation.EnableDiagnosticsFor(17);
        Assert.Null(simulation.LastDiagnosticSample);
        Assert.Same(initial, simulation.State); Assert.Equal(random, simulation.RandomState);
        simulation.AdvanceTick();
        var state = simulation.State; random = simulation.RandomState;
        var sample = Assert.IsType<OrganismDiagnosticSample>(simulation.LastDiagnosticSample);
        simulation.EnableDiagnosticsFor(17);
        Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.EnableDiagnosticsFor(23);
        Assert.Null(simulation.LastDiagnosticSample);
        simulation.EnableDiagnosticsFor(17); // Switching back must not reveal the old sample.
        Assert.Null(simulation.LastDiagnosticSample);
        simulation.DisableDiagnostics(); simulation.DisableDiagnostics();
        Assert.Null(simulation.LastDiagnosticSample); Assert.Null(simulation.DiagnosticOrganismId);
        Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState);
        simulation.EnableDiagnosticsFor(17);
        simulation.AdvanceTick();
        Assert.Equal(2, simulation.LastDiagnosticSample!.TickCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void AbsentOrDyingOrganismCommitsNullCommandsEveryTick(bool neural, bool absent)
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(id: 17, energy: 0.5)],
            settings: NeuralFixture.Settings(enabled: neural));
        simulation.EnableDiagnosticsFor(absent ? int.MaxValue : 17);
        for (var tick = 1; tick <= 3; tick++)
        {
            simulation.AdvanceTick();
            var sample = Assert.IsType<OrganismDiagnosticSample>(simulation.LastDiagnosticSample);
            Assert.Equal(tick, sample.TickCount);
            Assert.Equal(absent ? int.MaxValue : 17, sample.OrganismId);
            Assert.False(sample.DidAct);
            Assert.Equal(neural, sample.IsNeuralMode); Assert.Equal(!neural, sample.IsRuleBasedMode);
            NullCommands(sample);
        }
    }

    [Fact]
    public void RuleBasedSurvivorHasNoNeuralCommands()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism()], settings: NeuralFixture.Settings(enabled: false));
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        Assert.True(sample.DidAct); Assert.True(sample.IsRuleBasedMode); NullCommands(sample);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NewbornDoesNotActUntilTheFollowingTick(bool neural)
    {
        var simulation = NeuralMutationFixture.Create([NeuralMutationFixture.Parent(id: 17)],
            NeuralMutationFixture.Settings(enabled: neural));
        simulation.EnableDiagnosticsFor(18); simulation.AdvanceTick();
        Assert.Contains(simulation.State.World.Organisms, o => o.Id == 18);
        Assert.False(simulation.LastDiagnosticSample!.DidAct); NullCommands(simulation.LastDiagnosticSample);
        simulation.AdvanceTick();
        Assert.True(simulation.LastDiagnosticSample!.DidAct);
        Assert.Equal(2, simulation.LastDiagnosticSample.TickCount);
    }

    [Fact]
    public void CapturesPostDrainAndSequentialFoodAvailabilityRatherThanPostTickInputs()
    {
        // Independent scalar tanh calculation: hidden0 reads coincidence + post-drain
        // energy, output0 uses hidden0; output1 has a nonzero fixed bias.
        var weights = new double[74]; weights[2] = 0.4; weights[3] = 0.7;
        weights[56] = 0.8; weights[72] = 0.1; weights[73] = -0.3;
        var brain = new NeuralGenome(weights);
        var organism = NeuralFixture.Organism(brain, id: 17, speed: 0, energy: 60, metabolism: 10, heading: 0, steering: 0.5);
        var simulation = NeuralFixture.Create([organism], [new Food(9, 50, 40, 20)],
            NeuralFixture.Settings(food: 1));
        simulation.EnableDiagnosticsFor(17); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        var steering = Math.Tanh(0.1 + 0.8 * Math.Tanh(0.4)); // energy after drain=50 => channel3=0
        Assert.Equal(steering, sample.NeuralOutputSteering);
        Assert.Equal(Math.Tanh(-0.3), sample.NeuralOutputSpeed);
        Assert.Equal(steering * 0.5, sample.AppliedRotationRadians);
        Assert.Equal(0, sample.EffectiveSpeed);
        Assert.Equal(70, simulation.State.World.Organisms[0].Energy); // feeding is later
        Assert.Empty(simulation.State.World.FoodItems);

        var first = NeuralFixture.Organism(id: 2, speed: 0, metabolism: 0);
        var second = NeuralFixture.Organism(NeuralFixture.ChannelBrain(2), id: 17, speed: 0, metabolism: 0);
        simulation = NeuralFixture.Create([first, second], [new Food(9, 50, 40, 20)],
            NeuralFixture.Settings(organisms: 2, food: 1));
        simulation.EnableDiagnosticsFor(17); simulation.AdvanceTick();
        Assert.Equal(Math.Tanh(Math.Tanh(-1)), simulation.LastDiagnosticSample!.NeuralOutputSteering);
    }

    [Fact]
    public void CommandsAreCapturedBeforeReflectionAndAreNotDisplacementProxies()
    {
        var organism = NeuralFixture.Organism(NeuralFixture.Brain(0.2, 0.4), x: 99, y: 40, heading: 0, steering: 0.1);
        var simulation = NeuralFixture.Create([organism]);
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!;
        var rotation = Math.Tanh(0.2) * 0.1;
        var speed = 10 * (1 + 0.5 * Math.Tanh(0.4));
        Assert.Equal(rotation, sample.AppliedRotationRadians); Assert.Equal(speed, sample.EffectiveSpeed);
        var current = simulation.State.World.Organisms[0];
        Assert.Equal(Math.PI - rotation, current.HeadingRadians);
        Assert.NotEqual(speed, Math.Sqrt(Math.Pow(current.X - organism.X, 2) + Math.Pow(current.Y - organism.Y, 2)));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(8.0, -8.0)]
    [InlineData(-8.0, 8.0)]
    public void FiniteZeroAndSaturatedOutputsAreCapturedExactly(double steering, double speed)
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(steering, speed), speed: 0)]);
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        Assert.Equal(Math.Tanh(steering), simulation.LastDiagnosticSample!.NeuralOutputSteering);
        Assert.Equal(Math.Tanh(speed), simulation.LastDiagnosticSample.NeuralOutputSpeed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaptureThenConstructionFailureRollsBackAndRetryMatchesCleanRun(bool priorSample)
    {
        var settings = NeuralMutationFixture.Settings(foodCap: 100);
        var parents = new[] { NeuralMutationFixture.Parent(NeuralFixture.Brain(0.3, 0.5), energy: 500) };
        var simulation = NeuralMutationFixture.Create(parents, settings);
        var clean = NeuralMutationFixture.Create(parents, settings);
        simulation.EnableDiagnosticsFor(0); clean.EnableDiagnosticsFor(0);
        if (priorSample) { simulation.AdvanceTick(); clean.AdvanceTick(); }
        var sample = simulation.LastDiagnosticSample;
        simulation.FailWorldConstructionForTesting = true;
        NeuralFixture.Rollback<ArgumentNullException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.FailWorldConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        NeuralFixture.SameState(clean, simulation);
        Assert.Equal(clean.LastDiagnosticSample!.NeuralOutputSteering, simulation.LastDiagnosticSample!.NeuralOutputSteering);
        Assert.Equal(clean.TickCount, simulation.LastDiagnosticSample.TickCount);
    }

    [Fact]
    public void SteeringFailureAfterCaptureDoesNotPublishSample()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism()]);
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample;
        simulation.FailSteeringForOrganismIdForTesting = 0;
        NeuralFixture.Rollback<InvalidOperationException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReproductionFailureAfterCaptureAndDrawsPreservesSampleAndRetry(bool afterWeightDraws)
    {
        var settings = NeuralMutationFixture.Settings(foodCap: 100);
        var parents = new[] { NeuralMutationFixture.Parent(energy: 500) };
        var simulation = NeuralMutationFixture.Create(parents, settings);
        var clean = NeuralMutationFixture.Create(parents, settings);
        simulation.EnableDiagnosticsFor(0); clean.EnableDiagnosticsFor(0);
        simulation.AdvanceTick(); clean.AdvanceTick();
        var sample = simulation.LastDiagnosticSample;
        simulation.FailBeforeMutationForTesting = !afterWeightDraws;
        simulation.FailOffspringTraitConstructionForTesting = afterWeightDraws;
        if (afterWeightDraws) NeuralFixture.Rollback<ArgumentOutOfRangeException>(simulation);
        else NeuralFixture.Rollback<InvalidOperationException>(simulation);
        Assert.Same(sample, simulation.LastDiagnosticSample);
        simulation.FailBeforeMutationForTesting = simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); clean.AdvanceTick();
        NeuralFixture.SameState(clean, simulation);
        Assert.Equal(clean.LastDiagnosticSample!.TickCount, simulation.LastDiagnosticSample!.TickCount);
    }

    [Fact]
    public void SamplesAreSealedReadOnlyAndRemainUnchangedAfterLaterTicks()
    {
        Assert.True(typeof(OrganismDiagnosticSample).IsSealed);
        Assert.All(typeof(OrganismDiagnosticSample).GetProperties(), property => Assert.Null(property.SetMethod));
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.ChannelBrain(3))]);
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        var sample = simulation.LastDiagnosticSample!; var steering = sample.NeuralOutputSteering;
        simulation.AdvanceTick();
        Assert.NotSame(sample, simulation.LastDiagnosticSample);
        Assert.Equal(1, sample.TickCount); Assert.Equal(steering, sample.NeuralOutputSteering);
        Assert.NotEqual(steering, simulation.LastDiagnosticSample!.NeuralOutputSteering);
    }

    [Fact]
    public void DisabledNeuralCaptureAndObserverConfigurationAllocateNothing()
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism()]);
        var organism = simulation.State.World.Organisms[0];
        var food = Array.Empty<Food>(); var consumed = Array.Empty<bool>();
        for (var i = 0; i < 100; i++) NeuralControl.Evaluate(organism, 99, food, consumed, simulation.Settings);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            NeuralControl.Evaluate(organism, 99, food, consumed, simulation.Settings);
            simulation.EnableDiagnosticsFor(0); simulation.EnableDiagnosticsFor(0); simulation.DisableDiagnostics();
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Null(simulation.LastDiagnosticSample); Assert.Equal(0, simulation.TickCount);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(1.7976931348623157E+308, 2.0)]
    public void RotationTelemetryPreservesOverflowSafeSteeringLimit(double rate, double duration)
    {
        var simulation = NeuralFixture.Create([NeuralFixture.Organism(NeuralFixture.Brain(0.5), steering: rate, speed: 0, metabolism: 0)],
            settings: NeuralFixture.Settings(duration: duration));
        simulation.EnableDiagnosticsFor(0); simulation.AdvanceTick();
        Assert.Equal(Math.Tanh(0.5) * (rate == 0 ? 0 : Math.PI), simulation.LastDiagnosticSample!.AppliedRotationRadians);
    }

    [Fact]
    public void TickOverflowPublishesNothing()
    {
        var settings = NeuralFixture.Settings();
        var simulation = new Simulation(settings, new WorldState(100, 80, [NeuralFixture.Organism()], []), long.MaxValue);
        simulation.EnableDiagnosticsFor(0);
        NeuralFixture.Rollback<OverflowException>(simulation);
        Assert.Null(simulation.LastDiagnosticSample); Assert.Equal(0, simulation.DiagnosticOrganismId);
    }

    [Theory]
    [InlineData(true, 0.125)]
    [InlineData(true, 0.0)]
    [InlineData(false, 0.125)]
    public void DiagnosticRunMatchesAllExistingObservablesForSixHundredTicks(bool neural, double mutation)
    {
        var settings = NeuralMutationFixture.Settings(mutation: mutation, enabled: neural, organisms: 2,
            foodCap: 100, initialAmplitude: 0.5, traitMutation: true, duration: 0.1);
        var parents = new[] { NeuralFixture.Organism(NeuralFixture.Brain(0.2, -0.3), id: 7, energy: 500),
            NeuralFixture.Organism(NeuralFixture.Brain(-0.1, 0.1), id: 43, energy: 500) };
        var enabled = NeuralMutationFixture.Create(parents, settings);
        var disabled = NeuralMutationFixture.Create(parents, settings);
        enabled.EnableDiagnosticsFor(43);
        for (var tick = 1; tick <= 600; tick++)
        {
            enabled.AdvanceTick(); disabled.AdvanceTick();
            NeuralFixture.SameState(enabled, disabled);
            static IEnumerable<long> WorldBits(WorldState world)
            {
                yield return BitConverter.DoubleToInt64Bits(world.WorldWidth);
                yield return BitConverter.DoubleToInt64Bits(world.WorldHeight);
                foreach (var o in world.Organisms)
                    foreach (var value in new[] { o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate })
                        yield return BitConverter.DoubleToInt64Bits(value);
                foreach (var f in world.FoodItems)
                    foreach (var value in new[] { f.X, f.Y, f.Energy })
                        yield return BitConverter.DoubleToInt64Bits(value);
            }
            Assert.Equal(WorldBits(enabled.State.World), WorldBits(disabled.State.World));
            Assert.Equal(tick, enabled.LastDiagnosticSample!.TickCount);
            Assert.Null(disabled.LastDiagnosticSample);
        }
        Assert.True(enabled.GetStatistics().TotalBirths > 0);
        Assert.True(enabled.GetStatistics().TotalDeaths > 0);
    }
}
