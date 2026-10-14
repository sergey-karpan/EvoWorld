using System.Globalization;
using System.Text;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class PopulationHistoryCsvTests
{
    [Fact]
    public void RuleBasedDistinctInactiveGenomesExportBlankDiversityForBothSpecies()
    {
        var history = new PopulationHistory(PopulationHistoryTests.DistinctInactiveGenomes());
        history.Observe(PopulationHistoryTests.DistinctInactiveGenomes(100));
        var rows = Serialize(history.Capture()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var columns = rows[0].Split(',');
        Assert.Equal(3, rows.Length);
        Assert.All(rows.Skip(1), row =>
        {
            var values = row.Split(','); Assert.Equal(66, values.Length);
            Assert.Equal("", values[Array.IndexOf(columns, "HerbivoreMeanParameterVariance")]);
            Assert.Equal("", values[Array.IndexOf(columns, "PredatorMeanParameterVariance")]);
            Assert.Equal("RuleBased", values[Array.IndexOf(columns, "Controller")]);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportBomHasExactThreeBytesImmediatelyBeforeHeaderAndPreservesCrLf(bool existing)
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "history.csv");
            if (existing) File.WriteAllText(target, "previous");
            var history = new PopulationHistory(PopulationHistoryTests.Empty(duration: 0.1));
            history.Observe(PopulationHistoryTests.Empty(100, duration: 0.1));
            PopulationHistoryCsv.Export(target, history.Capture());
            var bytes = File.ReadAllBytes(target);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray()); Assert.Equal((byte)'T', bytes[3]);
            var body = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
            Assert.Equal(Serialize(history.Capture()), body); Assert.StartsWith(ExpectedHeader + "\r\n", body);
            Assert.DoesNotContain('\uFEFF', body);
            Assert.DoesNotContain("\n", body.Replace("\r\n", "")); Assert.DoesNotContain("\r", body.Replace("\r\n", ""));
            Assert.Equal(3, body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.All(body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries), row => Assert.Equal(66, row.Split(',').Length));
            Assert.Single(Directory.GetFiles(directory));
        });
    }

    private const string ExpectedHeader = "Tick,ModelTimeSeconds,OrganismCount,HerbivoreCount,PredatorCount,FoodCount,TotalBirths,TotalDeaths,TotalPredationEvents,BirthsSincePreviousSample,DeathsSincePreviousSample,PredationEventsSincePreviousSample,MissedSamplingBoundaries,"
        + "HerbivoreSpeedCount,HerbivoreSpeedMin,HerbivoreSpeedMax,HerbivoreSpeedMean,HerbivoreSpeedStandardDeviation,HerbivoreMetabolismCount,HerbivoreMetabolismMin,HerbivoreMetabolismMax,HerbivoreMetabolismMean,HerbivoreMetabolismStandardDeviation,HerbivoreSensingRadiusCount,HerbivoreSensingRadiusMin,HerbivoreSensingRadiusMax,HerbivoreSensingRadiusMean,HerbivoreSensingRadiusStandardDeviation,HerbivoreSteeringRateCount,HerbivoreSteeringRateMin,HerbivoreSteeringRateMax,HerbivoreSteeringRateMean,HerbivoreSteeringRateStandardDeviation,HerbivoreMeanParameterVariance,"
        + "PredatorSpeedCount,PredatorSpeedMin,PredatorSpeedMax,PredatorSpeedMean,PredatorSpeedStandardDeviation,PredatorMetabolismCount,PredatorMetabolismMin,PredatorMetabolismMax,PredatorMetabolismMean,PredatorMetabolismStandardDeviation,PredatorSensingRadiusCount,PredatorSensingRadiusMin,PredatorSensingRadiusMax,PredatorSensingRadiusMean,PredatorSensingRadiusStandardDeviation,PredatorSteeringRateCount,PredatorSteeringRateMin,PredatorSteeringRateMax,PredatorSteeringRateMean,PredatorSteeringRateStandardDeviation,PredatorMeanParameterVariance,"
        + "Seed,Controller,NeuralGenomeActive,NeuralMutationAmplitude,InitialPredatorCount,TickDurationSeconds,SamplingIntervalTicks,DroppedSamples,TotalMissedSamplingBoundaries,CoreVersion,RuntimeVersion";

    private static string Serialize(PopulationHistorySnapshot snapshot)
    {
        using var writer = new StringWriter(); PopulationHistoryCsv.Write(writer, snapshot); return writer.ToString();
    }

    [Fact]
    public void ExactStableHeaderHasAllSixtySixColumnsAndNoPreamble()
    {
        var history = new PopulationHistory(PopulationHistoryTests.Empty());
        var csv = Serialize(history.Capture()); Assert.Equal(ExpectedHeader, PopulationHistoryCsv.Header);
        Assert.StartsWith(ExpectedHeader + "\r\n", csv); Assert.Equal(66, csv.Split("\r\n")[1].Split(',').Length);
        Assert.Equal(66, ExpectedHeader.Split(',').Distinct().Count());
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    [InlineData("uk-UA")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void NonEnglishCulturesPreserveInvariantRoundTripDecimalsAndMetadata(string culture)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var history = new PopulationHistory(PopulationHistoryTests.Empty(duration: 0.1));
            history.Observe(PopulationHistoryTests.Empty(100, duration: 0.1));
            var rows = Serialize(history.Capture()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var header = rows[0].Split(','); var values = rows[2].Split(',');
            Assert.Equal(66, values.Length);
            Assert.Equal("100", values[0]); Assert.Equal("10", values[1]);
            Assert.Equal("0.1", values[Array.IndexOf(header, "TickDurationSeconds")]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(0.1), BitConverter.DoubleToInt64Bits(double.Parse(values[Array.IndexOf(header, "TickDurationSeconds")], CultureInfo.InvariantCulture)));
            Assert.Equal("42", values[Array.IndexOf(header, "Seed")]); Assert.Equal("RuleBased", values[Array.IndexOf(header, "Controller")]);
            Assert.Equal("false", values[Array.IndexOf(header, "NeuralGenomeActive")]); Assert.Equal("100", values[Array.IndexOf(header, "SamplingIntervalTicks")]);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void OverflowedModelTimeAndEmptyGenomeVarianceExportBlankNotSentinels()
    {
        var history = new PopulationHistory(PopulationHistoryTests.Empty(duration: double.MaxValue));
        history.Observe(PopulationHistoryTests.Empty(100, double.MaxValue));
        var rows = Serialize(history.Capture()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var columns = rows[0].Split(','); var record = rows[2].Split(',');
        Assert.Equal("", record[1]); Assert.Equal("", record[Array.IndexOf(columns, "HerbivoreMeanParameterVariance")]);
        Assert.Equal("", record[Array.IndexOf(columns, "PredatorMeanParameterVariance")]);
        Assert.DoesNotContain("NaN", rows[2]); Assert.DoesNotContain("Infinity", rows[2]);
    }

    [Fact]
    public void FrozenExportContainsRetainedRowsOnlyAndExplicitTruncationMetadata()
    {
        var history = new PopulationHistory(PopulationHistoryTests.Empty(), capacity: 2);
        history.Observe(PopulationHistoryTests.Empty(100)); history.Observe(PopulationHistoryTests.Empty(300));
        var frozen = history.Capture(); history.Observe(PopulationHistoryTests.Empty(400));
        var rows = Serialize(frozen).Split("\r\n", StringSplitOptions.RemoveEmptyEntries); var columns = rows[0].Split(',');
        Assert.Equal(3, rows.Length); Assert.Equal(new[] { "100", "300" }, rows.Skip(1).Select(r => r.Split(',')[0]));
        Assert.All(rows.Skip(1), r => Assert.Equal("1", r.Split(',')[Array.IndexOf(columns, "DroppedSamples")]));
        Assert.All(rows.Skip(1), r => Assert.Equal("1", r.Split(',')[Array.IndexOf(columns, "TotalMissedSamplingBoundaries")]));
        Assert.Equal(2, history.DroppedSamples); Assert.Equal(400, history[^1].Tick);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("a\r\nb", "\"a\r\nb\"")]
    public void MetadataQuotingFollowsRfc4180(string value, string expected) => Assert.Equal(expected, PopulationHistoryCsv.Quote(value));

    [Fact]
    public void VersionMetadataIsQuotedWithoutChangingFixedColumnLayout()
    {
        var history = new PopulationHistory(PopulationHistoryTests.Empty()); var snapshot = history.Capture();
        snapshot = snapshot with { Metadata = snapshot.Metadata with { CoreVersion = "1,2\"3", RuntimeVersion = "runtime,10" } };
        Assert.EndsWith(",\"1,2\"\"3\",\"runtime,10\"\r\n", Serialize(snapshot));
    }

    [Fact]
    public void WriterRejectsDuplicateTicksAndNonFiniteNumbersInsteadOfProducingMisleadingRows()
    {
        var history = new PopulationHistory(PopulationHistoryTests.Empty()); var snapshot = history.Capture();
        Assert.Throws<InvalidOperationException>(() => Serialize(snapshot with { Samples = new[] { snapshot.Samples[0], snapshot.Samples[0] } }));
        Assert.Throws<InvalidOperationException>(() => Serialize(snapshot with { Samples = new[] { snapshot.Samples[0] with { ModelTimeSeconds = double.NaN } } }));
    }

    [Fact]
    public void NewTargetAndExistingTargetArePublishedWithUtf8BomAndExactBytes()
    {
        InDirectory(directory =>
        {
            var history = new PopulationHistory(PopulationHistoryTests.Empty()); var snapshot = history.Capture();
            var target = Path.Combine(directory, "history.csv");
            PopulationHistoryCsv.Export(target, snapshot); var expected = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes(Serialize(snapshot))).ToArray();
            Assert.Equal(expected, File.ReadAllBytes(target)); Assert.Single(Directory.GetFiles(directory));
            history.Observe(PopulationHistoryTests.Empty(100)); PopulationHistoryCsv.Export(target, history.Capture());
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes(Serialize(history.Capture()))).ToArray(), File.ReadAllBytes(target));
            Assert.Single(Directory.GetFiles(directory)); Assert.Equal(2, history.Count);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedSerializationPreservesOriginalOrLeavesNewTargetAbsentAndCleansOnlyTemporarySibling(bool existing)
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "history.csv"); var other = Path.Combine(directory, "unrelated.txt");
            File.WriteAllText(other, "keep"); if (existing) File.WriteAllText(target, "original");
            var history = new PopulationHistory(PopulationHistoryTests.Empty()); var before = history.Capture();
            Assert.Throws<IOException>(() => PopulationHistoryCsv.Export(target, before,
                (writer, _) => { writer.Write("partial"); throw new IOException("Interrupted"); }, (_, _) => throw new Exception("Must not replace")));
            Assert.Equal(existing, File.Exists(target)); if (existing) Assert.Equal("original", File.ReadAllText(target));
            Assert.Equal("keep", File.ReadAllText(other)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            Assert.Equal(before.Samples, history.Capture().Samples); Assert.Equal(1, history.Revision);
        });
    }

    [Fact]
    public void AtomicReplacementFailureNeverFallsBackToOverwriteAndPreservesPreviousTarget()
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "history.csv"); File.WriteAllText(target, "previous target");
            var replacementCalls = 0;
            Assert.Throws<IOException>(() => PopulationHistoryCsv.Export(target, new PopulationHistory(PopulationHistoryTests.Empty()).Capture(),
                PopulationHistoryCsv.Write, (temporary, destination) =>
                {
                    replacementCalls++; Assert.Equal(target, destination);
                    Assert.Equal(Path.GetDirectoryName(destination), Path.GetDirectoryName(temporary));
                    Assert.StartsWith(ExpectedHeader, File.ReadAllText(temporary)); throw new IOException("Replacement failed");
                }));
            Assert.Equal(1, replacementCalls); Assert.Equal("previous target", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(directory));
        });
    }

    [Fact]
    public void NewTargetRaceDoesNotOverwriteAFileCreatedBeforeMove()
    {
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "history.csv");
            Assert.Throws<IOException>(() => PopulationHistoryCsv.Export(target, new PopulationHistory(PopulationHistoryTests.Empty()).Capture(),
                (writer, snapshot) => { PopulationHistoryCsv.Write(writer, snapshot); File.WriteAllText(target, "created by another writer"); }, (_, _) => throw new Exception("No replacement")));
            Assert.Equal("created by another writer", File.ReadAllText(target)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    [Fact]
    public void MissingOutputDirectoryFailsWithoutChangingSimulationOrHistory()
    {
        InDirectory(directory =>
        {
            var simulation = PopulationHistoryTests.Empty(); var state = simulation.State; var random = simulation.RandomState;
            var history = new PopulationHistory(simulation); var before = history.Capture();
            Assert.Throws<DirectoryNotFoundException>(() => PopulationHistoryCsv.Export(Path.Combine(directory, "missing", "out.csv"), before));
            Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Single(history);
            Assert.Equal(before.Samples, history.Capture().Samples); Assert.Empty(Directory.GetFiles(directory));
        });
    }

    [Fact]
    public void ExactInt64TickAndUInt64SeedAreNotRoundedThroughFloatingPoint()
    {
        var snapshot = new PopulationHistory(PopulationHistoryTests.Empty()).Capture();
        snapshot = snapshot with { Metadata = snapshot.Metadata with { Seed = ulong.MaxValue },
            Samples = new[] { snapshot.Samples[0] with { Tick = long.MaxValue, ModelTimeSeconds = null } } };
        var rows = Serialize(snapshot).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var columns = rows[0].Split(','); var row = rows[1].Split(',');
        Assert.Equal("9223372036854775807", row[0]);
        Assert.Equal("18446744073709551615", row[Array.IndexOf(columns, "Seed")]);
    }

    [Fact]
    public void ActualLockedTargetReplacementFailsWithoutDestroyingPreviousFile()
    {
        // Windows disallows replacement without delete sharing; Unix permits renaming
        // open files. The injected replacement-failure test is platform independent.
        if (!OperatingSystem.IsWindows()) return;
        InDirectory(directory =>
        {
            var target = Path.Combine(directory, "history.csv"); File.WriteAllText(target, "locked original");
            using var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.Throws<IOException>(() => PopulationHistoryCsv.Export(target, new PopulationHistory(PopulationHistoryTests.Empty()).Capture()));
            using var reader = new StreamReader(locked, leaveOpen: true); Assert.Equal("locked original", reader.ReadToEnd());
            Assert.Single(Directory.GetFiles(directory)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "EvoWorld-HistoryCsvTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); } // Only this test's freshly allocated, owned directory.
    }
}
