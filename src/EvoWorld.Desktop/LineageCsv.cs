using System.Globalization;
using System.IO;

namespace EvoWorld.Desktop;

internal static class LineageCsv
{
    internal const string Header = "Seed,EffectiveMode,EffectiveNeuralMutationAmplitude,InitialPredatorCount,TimestepSeconds,DroppedLineageRecords,CapacityTruncated,OrganismId,Species,ParentId,BirthTick,DeathTick,IsAliveAtLastObservation,DirectChildCount,CompletenessFlag";
    internal static void Export(string target, LineageSnapshot snapshot) => PopulationHistoryCsv.ExportAtomic(target, snapshot, Write);

    internal static void Write(TextWriter writer, LineageSnapshot snapshot)
    {
        writer.Write(Header); writer.Write("\r\n");
        var m = snapshot.Metadata;
        if (!double.IsFinite(m.MutationAmplitude) || !double.IsFinite(m.TimestepSeconds)) throw new InvalidOperationException("Nonfinite lineage metadata.");
        foreach (var r in snapshot.Records)
        {
            string[] row = [m.Seed.ToString(CultureInfo.InvariantCulture), m.Neural ? "Neural" : "Rule-based",
                m.MutationAmplitude.ToString("R", CultureInfo.InvariantCulture), Integer(m.InitialPredatorCount),
                m.TimestepSeconds.ToString("R", CultureInfo.InvariantCulture), Integer(snapshot.DroppedLineageRecords), Boolean(snapshot.CapacityTruncated),
                Integer(r.OrganismId), r.Species.ToString(), Integer(r.ParentId), Integer(r.BirthTick), Integer(r.DeathTick),
                Boolean(r.IsAliveAtLastObservation), Integer(r.DirectChildCount), LineageLedger.Flags(r.CompletenessFlag)];
            writer.Write(string.Join(',', row)); writer.Write("\r\n");
        }
    }
    private static string Integer(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Boolean(bool value) => value ? "true" : "false";
}
