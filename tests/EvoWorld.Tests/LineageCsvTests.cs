using System.Globalization;
using System.Text;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class LineageCsvTests
{
    private const string ExpectedHeader = "Seed,EffectiveMode,EffectiveNeuralMutationAmplitude,InitialPredatorCount,TimestepSeconds,DroppedLineageRecords,CapacityTruncated,OrganismId,Species,ParentId,BirthTick,DeathTick,IsAliveAtLastObservation,DirectChildCount,CompletenessFlag";
    private static string Serialize(LineageSnapshot snapshot)
    {
        using var writer = new StringWriter(); LineageCsv.Write(writer, snapshot); return writer.ToString();
    }
    private static LineageSnapshot Snapshot()
    {
        var ledger = new LineageLedger(LineageLedgerTests.At(0, ParentageTests.Organism(7)));
        ledger.Observe(LineageLedgerTests.At(1, ParentageTests.Organism(7), ParentageTests.Organism(43, 7)));
        return ledger.Capture();
    }

    [Fact]
    public void FixedFifteenColumnSchemaAndNullableFieldsHaveExactIndependentRows()
    {
        var rows = Serialize(Snapshot()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ExpectedHeader, rows[0]); Assert.Equal(15, rows[0].Split(',').Length);
        Assert.Equal("42,Rule-based,0,0,1,0,false,7,Herbivore,,0,,true,1,Complete", rows[1]);
        Assert.Equal("42,Rule-based,0,0,1,0,false,43,Herbivore,7,1,,true,0,Complete", rows[2]);
        Assert.DoesNotContain("RuntimeVersion", rows[0]);
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("uk-UA")]
    [InlineData("de-DE")]
    public void UnsignedSeedAndRoundTripMetadataStayInvariantInCommaDecimalLocales(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var snapshot = Snapshot(); snapshot = snapshot with { Metadata = new(ulong.MaxValue, true, 0.1, 5, 1.0 / 60) };
            var row = Serialize(snapshot).Split("\r\n")[1].Split(',');
            Assert.Equal("18446744073709551615", row[0]); Assert.Equal("Neural", row[1]); Assert.Equal("0.1", row[2]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(1.0 / 60), BitConverter.DoubleToInt64Bits(double.Parse(row[4], CultureInfo.InvariantCulture)));
            Assert.Equal(15, row.Length);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void GapRowsDistinguishLivingFromUnknownDeathAndUnknownCountsAreBlank()
    {
        var ledger = new LineageLedger(LineageLedgerTests.At(0, ParentageTests.Organism(7)));
        ledger.Observe(LineageLedgerTests.At(3, ParentageTests.Organism(43, 7)));
        var rows = Serialize(ledger.Capture()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith(",7,Herbivore,,0,,false,,MissingDeathTick|ObservationGap", rows[1]);
        Assert.EndsWith(",43,Herbivore,7,,,true,,MissingBirthTick|ObservationGap", rows[2]);
    }

    [Fact]
    public void FrozenScalarSnapshotRetainsRowsMetadataAndTruncationWithoutMarkingUnrelatedSurvivorsIncomplete()
    {
        var parent = ParentageTests.Organism(7); var ledger = new LineageLedger(LineageLedgerTests.At(0, parent), 1);
        var frozen = ledger.Capture(); var bytes = Serialize(frozen);
        ledger.Observe(LineageLedgerTests.At(1, ParentageTests.Organism(43, 7)));
        Assert.Equal(bytes, Serialize(frozen)); Assert.Equal(0, frozen.DroppedLineageRecords);
        var row = Serialize(ledger.Capture()).Split("\r\n")[1].Split(',');
        Assert.Equal("1", row[5]); Assert.Equal("true", row[6]); Assert.Equal("Complete", row[14]); Assert.Equal("7", row[9]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewAndReplacementExportsHaveExactBomCrLfAndUnchangedScalarBytes(bool existing)
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "lineage.csv"); if (existing) File.WriteAllText(target, "old");
            var snapshot = Snapshot(); LineageCsv.Export(target, snapshot);
            var expected = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Serialize(snapshot))).ToArray();
            var actual = File.ReadAllBytes(target); Assert.Equal(expected, actual); Assert.Equal((byte)'S', actual[3]);
            Assert.Single(Directory.GetFiles(directory));
            var text = File.ReadAllText(target); Assert.DoesNotContain("\n", text.Replace("\r\n", "")); Assert.DoesNotContain("\r", text.Replace("\r\n", ""));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedAtomicWriterFailurePreservesPreviousFileOrLeavesNewTargetAbsent(bool existing)
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "lineage.csv"); if (existing) File.WriteAllText(target, "original");
            Assert.Throws<IOException>(() => PopulationHistoryCsv.ExportAtomic(target, Snapshot(),
                (writer, _) => { writer.Write("partial"); throw new IOException("Injected write failure"); }));
            Assert.Equal(existing, File.Exists(target)); if (existing) Assert.Equal("original", File.ReadAllText(target));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    [Fact]
    public void SharedReplacementFailureHasNoDestructiveFallback()
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "lineage.csv"); File.WriteAllText(target, "original"); var calls = 0;
            Assert.Throws<IOException>(() => PopulationHistoryCsv.ExportAtomic(target, Snapshot(), LineageCsv.Write,
                (temporary, destination) => { calls++; Assert.StartsWith(ExpectedHeader, File.ReadAllText(temporary)); throw new IOException("Replace failure"); }));
            Assert.Equal(1, calls); Assert.Equal("original", File.ReadAllText(target)); Assert.Single(Directory.GetFiles(directory));
        });
    }

    [Fact]
    public void ActualLockedTargetFailurePreservesFileAndObservations()
    {
        if (!OperatingSystem.IsWindows()) return;
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "lineage.csv"); File.WriteAllText(target, "original"); var snapshot = Snapshot();
            using (var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => LineageCsv.Export(target, snapshot));
                using var reader = new StreamReader(stream); Assert.Equal("original", reader.ReadToEnd());
            }
            Assert.Single(Directory.GetFiles(directory)); Assert.Equal(1, snapshot.LastObservedTick);
        });
    }

    private static void InDirectory(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "EvoWorld-LineageTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { action(path); }
        finally { Directory.Delete(path, true); } // Only this test's unique, newly created directory.
    }
}
