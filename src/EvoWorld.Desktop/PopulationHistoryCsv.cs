using System.IO;
using System.Globalization;
using System.Text;

namespace EvoWorld.Desktop;

internal static class PopulationHistoryCsv
{
    private static readonly string[] FixedColumns = ["Tick", "ModelTimeSeconds", "OrganismCount", "HerbivoreCount", "PredatorCount", "FoodCount",
        "TotalBirths", "TotalDeaths", "TotalPredationEvents", "BirthsSincePreviousSample", "DeathsSincePreviousSample",
        "PredationEventsSincePreviousSample", "MissedSamplingBoundaries"];
    private static readonly string[] MetadataColumns = ["Seed", "Controller", "NeuralGenomeActive", "NeuralMutationAmplitude", "InitialPredatorCount",
        "TickDurationSeconds", "SamplingIntervalTicks", "DroppedSamples", "TotalMissedSamplingBoundaries", "CoreVersion", "RuntimeVersion"];
    internal static string Header { get; } = string.Join(',', Columns());

    private static IEnumerable<string> Columns()
    {
        foreach (var name in FixedColumns) yield return name;
        foreach (var species in new[] { "Herbivore", "Predator" })
        {
            foreach (var trait in new[] { "Speed", "Metabolism", "SensingRadius", "SteeringRate" })
                foreach (var field in new[] { "Count", "Min", "Max", "Mean", "StandardDeviation" }) yield return species + trait + field;
            yield return species + "MeanParameterVariance";
        }
        foreach (var name in MetadataColumns) yield return name;
    }

    internal static void Write(TextWriter writer, PopulationHistorySnapshot snapshot)
    {
        writer.Write(Header); writer.Write("\r\n");
        long? lastTick = null;
        foreach (var sample in snapshot.Samples)
        {
            if (lastTick is long previous && sample.Tick <= previous) throw new InvalidOperationException("CSV samples must be ascending and unique.");
            lastTick = sample.Tick;
            var values = new List<string>(66)
            {
                Integer(sample.Tick), Number(sample.ModelTimeSeconds), Integer(sample.OrganismCount), Integer(sample.HerbivoreCount),
                Integer(sample.PredatorCount), Integer(sample.FoodCount), Integer(sample.TotalBirths), Integer(sample.TotalDeaths),
                Integer(sample.TotalPredationEvents), Integer(sample.BirthsSincePreviousSample), Integer(sample.DeathsSincePreviousSample),
                Integer(sample.PredationEventsSincePreviousSample), Integer(sample.MissedSamplingBoundaries)
            };
            Species(values, sample.Herbivores); Species(values, sample.Predators);
            var metadata = snapshot.Metadata;
            values.AddRange([metadata.Seed.ToString(CultureInfo.InvariantCulture), metadata.Controller.ToString(),
                metadata.NeuralGenomeActive ? "true" : "false", Number(metadata.NeuralMutationAmplitude), Integer(metadata.InitialPredatorCount),
                Number(metadata.TickDurationSeconds), Integer(metadata.SamplingIntervalTicks), Integer(snapshot.DroppedSamples),
                Integer(snapshot.TotalMissedSamplingBoundaries), Quote(metadata.CoreVersion), Quote(metadata.RuntimeVersion)]);
            writer.Write(string.Join(',', values)); writer.Write("\r\n");
        }
    }

    private static void Species(List<string> values, SpeciesHistorySummary species)
    {
        foreach (var trait in new[] { species.Speed, species.Metabolism, species.SensingRadius, species.SteeringRate })
            values.AddRange([Integer(trait.Count), Number(trait.Min), Number(trait.Max), Number(trait.Mean), Number(trait.StandardDeviation)]);
        values.Add(Number(species.MeanParameterVariance));
    }
    private static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Number(double? value)
    {
        if (value is not double number) return "";
        if (!double.IsFinite(number)) throw new InvalidOperationException("CSV numbers must be finite or null.");
        return number.ToString("R", CultureInfo.InvariantCulture);
    }
    internal static string Quote(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0
        ? value : "\"" + value.Replace("\"", "\"\"") + "\"";

    // No replacement fallback: a failed File.Replace must leave the previous target intact.
    internal static void Export(string target, PopulationHistorySnapshot snapshot) => Export(target, snapshot, Write,
        static (temporary, destination) => File.Replace(temporary, destination, null));

    // Narrow file-operation seams test interrupted serialization and failed atomic replacement.
    internal static void Export(string target, PopulationHistorySnapshot snapshot,
        Action<TextWriter, PopulationHistorySnapshot> write, Action<string, string> replace)
        => ExportAtomic(target, snapshot, write, replace);

    internal static void ExportAtomic<T>(string target, T snapshot, Action<TextWriter, T> write) => ExportAtomic(target, snapshot, write,
        static (temporary, destination) => File.Replace(temporary, destination, null));

    internal static void ExportAtomic<T>(string target, T snapshot,
        Action<TextWriter, T> write, Action<string, string> replace)
    {
        var destination = Path.GetFullPath(target);
        var replaceExisting = File.Exists(destination);
        var directory = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 4096, leaveOpen: true))
                {
                    write(writer, snapshot); writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            if (replaceExisting) replace(temporary, destination);
            else File.Move(temporary, destination); // No overwrite; races fail safely.
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary); // Only our unique sibling is eligible for cleanup.
        }
    }
}
