using EvoWorld.Core;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class LineageLedgerTests
{
    internal static Simulation At(long tick, params Organism[] organisms) => new(
        PredationFixture.Settings(herbivores: organisms.Count(o => o.Species == Species.Herbivore), predators: organisms.Count(o => o.Species == Species.Predator)),
        new WorldState(100, 80, organisms.OrderBy(o => o.Id), []), tick);

    [Fact]
    public void AdjacentBirthsAndDeathsAreExactAndRepeatedObservationsAreNoOps()
    {
        var parent = ParentageTests.Organism(7); var child = ParentageTests.Organism(43, 7);
        var ledger = new LineageLedger(At(0, parent)); var initial = ledger.Capture();
        Assert.Equal(0, ledger.Find(7)!.BirthTick); Assert.Equal(0, ledger.Find(7)!.DirectChildCount);
        ledger.Observe(At(1, parent, child)); var born = ledger.Capture();
        Assert.False(ledger.Observe(At(1, parent, child))); Assert.Same(born, ledger.Capture());
        Assert.Equal(1, ledger.Find(7)!.DirectChildCount); Assert.Equal(1, ledger.Find(43)!.BirthTick);
        ledger.Observe(At(2, child));
        Assert.False(ledger.Find(7)!.IsAliveAtLastObservation); Assert.Equal(2, ledger.Find(7)!.DeathTick);
        Assert.Equal(7, ledger.Find(43)!.ParentId); Assert.Null(ledger.Find(43)!.DeathTick);
        Assert.Equal(LineageCompleteness.Complete, ledger.Find(43)!.CompletenessFlag);
        Assert.Single(initial.Records); Assert.True(initial.Records[0].IsAliveAtLastObservation);
        Assert.Equal(0, initial.Records[0].DirectChildCount);
    }

    [Fact]
    public void GapsConservativelyInvalidateAllExistingCountsAndUnknownNewbornTimes()
    {
        var parent = ParentageTests.Organism(7); var disappeared = ParentageTests.Organism(9);
        var ledger = new LineageLedger(At(0, parent, disappeared));
        ledger.Observe(At(5, parent, ParentageTests.Organism(43, 7)));
        Assert.All(ledger.Capture().Records, r => { Assert.Null(r.DirectChildCount); Assert.True(r.CompletenessFlag.HasFlag(LineageCompleteness.ObservationGap)); });
        Assert.Null(ledger.Find(9)!.DeathTick); Assert.False(ledger.Find(9)!.IsAliveAtLastObservation);
        Assert.True(ledger.Find(9)!.CompletenessFlag.HasFlag(LineageCompleteness.MissingDeathTick));
        Assert.False(ledger.Find(7)!.CompletenessFlag.HasFlag(LineageCompleteness.MissingDeathTick));
        Assert.Null(ledger.Find(43)!.BirthTick); Assert.Equal(7, ledger.Find(43)!.ParentId);
        ledger.Observe(At(6, parent, ParentageTests.Organism(43, 7), ParentageTests.Organism(44, 7)));
        Assert.Null(ledger.Find(7)!.DirectChildCount); Assert.Equal(6, ledger.Find(44)!.BirthTick); Assert.Equal(0, ledger.Find(44)!.DirectChildCount);
    }

    [Fact]
    public void ProgressedInitialWorldHasUnknownBirthsAndCountsAndExtinctionIsObservable()
    {
        var ledger = new LineageLedger(At(100, ParentageTests.Organism(7)));
        Assert.Null(ledger.Find(7)!.BirthTick); Assert.Null(ledger.Find(7)!.DirectChildCount);
        ledger.Observe(At(101)); Assert.Equal(101, ledger.Find(7)!.DeathTick);
        ledger.Observe(At(102)); Assert.Equal(102, ledger.LastObservedTick);
    }

    [Fact]
    public void CapacityEvictsOnlyOldestDeadRecordsWithoutRewritingParentageOrCounts()
    {
        var parent = ParentageTests.Organism(7); var child = ParentageTests.Organism(43, 7);
        var ledger = new LineageLedger(At(0, parent), capacity: 2);
        ledger.Observe(At(1, parent, child));
        ledger.Observe(At(2, child, ParentageTests.Organism(44, 43)));
        Assert.Null(ledger.Find(7)); Assert.Equal(7, ledger.Find(43)!.ParentId);
        Assert.Equal(1, ledger.Find(43)!.DirectChildCount); Assert.Equal(1, ledger.DroppedLineageRecords);
        Assert.True(ledger.CapacityTruncated); Assert.Equal(LineageCompleteness.Complete, ledger.Find(43)!.CompletenessFlag);
        ledger.Observe(At(3, ParentageTests.Organism(44, 43), ParentageTests.Organism(45, 44)));
        Assert.Null(ledger.Find(43)); Assert.Equal(43, ledger.Find(44)!.ParentId);
        Assert.Equal(new[] { 44, 45 }, ledger.Capture().Records.Select(r => r.OrganismId));
    }

    [Fact]
    public void EvictingAChildDoesNotDecrementRetainedParentCount()
    {
        var parent = ParentageTests.Organism(7); var child = ParentageTests.Organism(43, 7);
        var ledger = new LineageLedger(At(0, parent), capacity: 2);
        ledger.Observe(At(1, parent, child)); ledger.Observe(At(2, parent, ParentageTests.Organism(44, 7)));
        Assert.Null(ledger.Find(43)); Assert.Equal(2, ledger.Find(7)!.DirectChildCount);
    }

    [Fact]
    public void CapacityFailureIsNamedAtomicAndRecoveryUsesGapRules()
    {
        var parent = ParentageTests.Organism(7); var ledger = new LineageLedger(At(0, parent), capacity: 1);
        var before = ledger.Capture();
        Assert.Throws<LineageLedgerCapacityException>(() => ledger.Observe(At(1, parent, ParentageTests.Organism(43, 7))));
        Assert.Same(before, ledger.Capture()); Assert.Equal(0, ledger.LastObservedTick); Assert.Equal(0, ledger.DroppedLineageRecords);
        ledger.Observe(At(2, ParentageTests.Organism(43, 7)));
        Assert.Equal(1, ledger.DroppedLineageRecords); Assert.Null(ledger.Find(43)!.BirthTick); Assert.Null(ledger.Find(43)!.DirectChildCount);
        Assert.Equal(7, ledger.Find(43)!.ParentId); Assert.True(ledger.Find(43)!.CompletenessFlag.HasFlag(LineageCompleteness.ObservationGap));
    }

    [Fact]
    public void ActualMaximumCapacityIsEnforcedWithoutEvictingLivingRecords()
    {
        var population = Enumerable.Range(0, LineageLedger.MaxLineageRecords).Select(i => ParentageTests.Organism(i)).ToArray();
        var ledger = new LineageLedger(At(0, population)); var before = ledger.Capture();
        var larger = population.Append(ParentageTests.Organism(LineageLedger.MaxLineageRecords)).ToArray();
        Assert.Throws<LineageLedgerCapacityException>(() => ledger.Observe(At(1, larger)));
        Assert.Same(before, ledger.Capture()); Assert.Equal(100_000, before.Records.Count);
    }

    [Fact]
    public void LongMaxTicksAndBackwardsObservationsAreOverflowSafe()
    {
        var ledger = new LineageLedger(At(long.MaxValue - 1, ParentageTests.Organism(7)));
        ledger.Observe(At(long.MaxValue)); Assert.Equal(long.MaxValue, ledger.Find(7)!.DeathTick);
        Assert.False(ledger.Observe(At(long.MaxValue)));
        var before = ledger.Capture(); Assert.Throws<InvalidOperationException>(() => ledger.Observe(At(long.MaxValue - 1)));
        Assert.Same(before, ledger.Capture());
    }

    [Fact]
    public void ProvenanceMismatchCannotPublishPartialChanges()
    {
        var ledger = new LineageLedger(At(0, ParentageTests.Organism(7))); var before = ledger.Capture();
        Assert.Throws<InvalidOperationException>(() => ledger.Observe(At(1, ParentageTests.Organism(7, 999))));
        Assert.Same(before, ledger.Capture());
    }

    [Fact]
    public void DesktopCapacityErrorsDoNotUndoCoreOrStopTicksAndResetIsTransactional()
    {
        var failReset = false;
        var experiment = new DesktopExperiment(_ => PredationFixture.Create([ParentageTests.Organism(7, energy: 300)], radius: 0, cap: 3),
            s => new PopulationHistory(s), s => failReset ? throw new InvalidOperationException("Reset ledger failure") : new LineageLedger(s, 1));
        var ledger = experiment.Lineage; var before = ledger.Capture();
        experiment.AdvanceTick(); Assert.Equal(1, experiment.Simulation.TickCount); Assert.Same(before, ledger.Capture());
        Assert.Contains("Lineage observation failed at tick 1", experiment.LineageObservationError);
        experiment.AdvanceTick(); Assert.Equal(2, experiment.Simulation.TickCount);
        experiment.Inspection.Select(experiment.Simulation, 7); failReset = true;
        var simulation = experiment.Simulation; var history = experiment.History;
        Assert.Throws<InvalidOperationException>(experiment.Reset);
        Assert.Same(simulation, experiment.Simulation); Assert.Same(ledger, experiment.Lineage); Assert.Same(history, experiment.History);
        Assert.Equal(7, experiment.Inspection.SelectedId);
        failReset = false; experiment.Reset(); Assert.NotSame(ledger, experiment.Lineage); Assert.Equal(0, experiment.Lineage.LastObservedTick);
        Assert.Null(experiment.Inspection.SelectedId); Assert.Null(experiment.LineageObservationError);
    }

    [Fact]
    public void FailedCoreTickAndInspectionRepaintsDoNotObserveTentativeBirths()
    {
        var experiment = new DesktopExperiment(_ => PredationFixture.Create([ParentageTests.Organism(7, energy: 200)], cap: 2));
        var before = experiment.Lineage.Capture(); experiment.Simulation.FailWorldConstructionForTesting = true;
        Assert.Throws<ArgumentNullException>(experiment.AdvanceTick); Assert.Same(before, experiment.Lineage.Capture());
        experiment.Inspection.Select(experiment.Simulation, 7);
        for (var i = 0; i < 3; i++) { experiment.Inspection.Observe(experiment.Simulation); experiment.Inspection.Format(experiment.Simulation); }
        Assert.Same(before, experiment.Lineage.Capture()); experiment.Simulation.FailWorldConstructionForTesting = false;
        experiment.AdvanceTick(); Assert.Equal(1, experiment.Lineage.Find(7)!.DirectChildCount);
    }

    [Fact]
    public void FlagsHaveFixedNonemptyOrderingAndUnknownDiffersFromFounder()
    {
        Assert.Equal("Complete", LineageLedger.Flags(LineageCompleteness.Complete));
        Assert.Equal("MissingBirthTick|MissingDeathTick|CapacityTruncated|ObservationGap", LineageLedger.Flags((LineageCompleteness)15));
        var ledger = new LineageLedger(At(0, ParentageTests.Organism(7)));
        Assert.Contains("Parent: Founder", ledger.Format(7)); Assert.Contains("Direct children: 0", ledger.Format(7));
        Assert.Contains("Parent: Unknown", ledger.Format(999)); Assert.Contains("Direct children: unavailable", ledger.Format(999));
    }

    [Fact]
    public void RepeatedBirthsByAnEarlierBornParentAreCountedExactlyOnce()
    {
        var founder = ParentageTests.Organism(7); var parent = ParentageTests.Organism(43, 7);
        var ledger = new LineageLedger(At(0, founder));
        ledger.Observe(At(1, founder, parent));
        ledger.Observe(At(2, parent, ParentageTests.Organism(44, 43)));
        ledger.Observe(At(3, parent, ParentageTests.Organism(44, 43), ParentageTests.Organism(45, 43)));
        Assert.Equal(1, ledger.Find(7)!.DirectChildCount); Assert.Equal(2, ledger.Find(43)!.DirectChildCount);
        Assert.Equal(1, ledger.Find(43)!.BirthTick); Assert.Equal(2, ledger.Find(7)!.DeathTick);
    }
}
