using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EvoWorld.Core;
using EvoWorld.Core.Random;
using EvoWorld.Desktop;
using Xunit;

namespace EvoWorld.Tests;

public sealed class AttackTraitTests
{
    // Captured before production edits from e79f8166e253949944bb2765e8d83ee98cb7d26c,
    // SDK 10.0.401 / runtime 10.0.12. The projection and capture source remain here.
    [Theory]
    [InlineData(ControllerMode.RuleBased)]
    [InlineData(ControllerMode.Neural)]
    internal void ProjectedLegacyStateAndCsvMatchPreTaskReference(ControllerMode mode)
    {
        var result = ProjectLegacy(mode, 5500);
        Assert.Equal(mode == ControllerMode.Neural ? NeuralReference : RuleReference, result);
    }

    private static readonly string[] RuleReference = ["95E990623563995BAEABC19E50A7B8D30296E19E82C2681C03DF1553B327D8CF",
        "5CC664306431E99205FE1C2B3DFCAF6DC7AE44AE96E4E2D5C1726D9C53497049", "828502CD910F30FC07102A878CBF4E4F6552C2F0538F4A410232BDE64B7C331F"];
    private static readonly string[] NeuralReference = ["250E04ED38FBD79F02FC54F41AE795D5672560DD7E3447B7D428EBC1607804F6",
        "17FC9CCA3C092A136491C577AEC7777AD973028CC4758011ABFC72219E1C6DA2", "EC0F71C66E9D64D4405F55ECB386D49049212BFEABAE5ADA151C431775256A8D"];

    internal static Organism Organism(int id = 7, double radius = 8, double efficiency = 1, Species species = Species.Herbivore,
        int? parent = null, double energy = 200, double x = 50, double speed = 0, double metabolism = 0) =>
        new(id, x, 40, 0, energy, speed, metabolism, 0, 0, EvoWorld.Core.Organism.ZeroBrain, species, parent, radius, efficiency);

    [Fact]
    public void TraitsAreReadOnlyAndEveryLegacyConstructorUsesExactCompatibilityDefaults()
    {
        Assert.Null(typeof(Organism).GetProperty("AttackRadius")!.SetMethod);
        Assert.Null(typeof(Organism).GetProperty("AttackEfficiency")!.SetMethod);
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = 7, ["x"] = 50.0, ["y"] = 40.0, ["headingRadians"] = 0.0, ["energy"] = 200.0,
            ["speed"] = 10.0, ["metabolism"] = 1.0, ["sensingRadius"] = 30.0, ["steeringRate"] = 2.0,
            ["brain"] = EvoWorld.Core.Organism.ZeroBrain, ["species"] = Species.Predator, ["parentId"] = 999
        };
        var constructors = typeof(Organism).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Where(c => c.GetParameters().Length <= 12).ToArray();
        Assert.Equal(new[] { 5, 7, 9, 10, 11, 12 }, constructors.Select(c => c.GetParameters().Length).Order());
        foreach (var constructor in constructors)
        {
            var o = (Organism)constructor.Invoke(constructor.GetParameters().Select(p => values[p.Name!]).ToArray());
            Assert.Equal(8, o.AttackRadius); Assert.Equal(1, o.AttackEfficiency); Assert.Equal(7, o.Id);
        }
    }

    [Theory]
    [InlineData(-1.0, "attackRadius")]
    [InlineData(double.NaN, "attackRadius")]
    [InlineData(double.PositiveInfinity, "attackRadius")]
    [InlineData(double.NegativeInfinity, "attackRadius")]
    [InlineData(-double.Epsilon, "attackEfficiency")]
    [InlineData(1.0000000000000002, "attackEfficiency")]
    [InlineData(double.NaN, "attackEfficiency")]
    [InlineData(double.PositiveInfinity, "attackEfficiency")]
    [InlineData(double.NegativeInfinity, "attackEfficiency")]
    public void TraitValidationNamesTheInvalidParameter(double value, string parameter) =>
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() =>
            Organism(radius: parameter == "attackRadius" ? value : 8, efficiency: parameter == "attackEfficiency" ? value : 1)).ParamName);

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(double.Epsilon, double.Epsilon)]
    [InlineData(double.MaxValue, 1.0)]
    public void GenericOrganismsAcceptFiniteBoundaryAndOutsideSettingsValues(double radius, double efficiency)
    {
        var o = Organism(radius: radius, efficiency: efficiency); Assert.Equal(radius, o.AttackRadius); Assert.Equal(efficiency, o.AttackEfficiency);
        var simulation = new Simulation(PredationFixture.Settings(herbivores: 1, predators: 0), new WorldState(100, 80, [o], []));
        simulation.AdvanceTick(); var survivor = Assert.Single(simulation.State.World.Organisms);
        Assert.Equal(radius, survivor.AttackRadius); Assert.Equal(efficiency, survivor.AttackEfficiency);
    }

    [Theory]
    [InlineData(false, 0.0, 6)]
    [InlineData(false, 0.1, 6)]
    [InlineData(true, 0.0, 6)]
    [InlineData(true, 0.1, 80)]
    public void EachParentCopiesItsOwnOutOfBoundsTraitsWithoutClampingMutationOrNewDraws(bool neural, double neuralMutation, int draws)
    {
        var a = Organism(7, 123, 0.02, parent: 999, energy: 300);
        var b = Organism(43, 0, 1, Species.Predator, parent: 9, energy: 300);
        var legacy = PredationFixture.Settings(herbivores: 1, predators: 1, cap: 6, radius: 0, neural: neural, mutation: neuralMutation);
        var settings = AttackSettings.Copy(legacy, minRadius: 2, minEfficiency: 0.2, radiusMutation: double.MaxValue, efficiencyMutation: double.MaxValue);
        var simulation = new Simulation(settings, new WorldState(100, 80, [a, b], []));
        var reference = new Pcg32(simulation.RandomState); var oldWorld = simulation.State.World;
        for (var tick = 1; tick <= 2; tick++)
        {
            simulation.AdvanceTick(); for (var i = 0; i < 2 * draws; i++) reference.NextUInt();
            Assert.Equal(reference.CaptureState(), simulation.RandomState);
            foreach (var o in simulation.State.World.Organisms)
            {
                var source = o.Id is 7 or 43 ? o.Id : o.ParentId!.Value;
                var parent = source == 7 ? a : b;
                Assert.Equal(BitConverter.DoubleToInt64Bits(parent.AttackRadius), BitConverter.DoubleToInt64Bits(o.AttackRadius));
                Assert.Equal(BitConverter.DoubleToInt64Bits(parent.AttackEfficiency), BitConverter.DoubleToInt64Bits(o.AttackEfficiency));
                Assert.Equal(parent.Species, o.Species);
            }
        }
        Assert.Equal(new[] { 7, 43, 44, 45, 46, 47 }, simulation.State.World.Organisms.Select(o => o.Id));
        Assert.Equal(999, simulation.State.World.Organisms[0].ParentId); Assert.Equal(9, simulation.State.World.Organisms[1].ParentId);
        Assert.Equal(300, oldWorld.Organisms[0].Energy); Assert.Equal(123, oldWorld.Organisms[0].AttackRadius);
        var full = simulation.RandomState; simulation.AdvanceTick(); Assert.Equal(full, simulation.RandomState);
    }

    [Fact]
    public void AnEarlierBornParentRetainsItsInheritedTraitsWhenItsOwnParentIsAbsent()
    {
        var settings = PredationFixture.Settings(herbivores: 1, predators: 0, cap: 3, radius: 0);
        var initial = new Simulation(settings, new WorldState(100, 80, [Organism(radius: 123, efficiency: 0.02)], []));
        initial.AdvanceTick(); var child = initial.State.World.Organisms.Single(o => o.ParentId == 7);
        // A custom progressed snapshot supplies energy to an actual earlier-born child,
        // with its immutable parentage and traits preserved and its founder absent.
        var fundedChild = new Organism(child.Id, child.X, child.Y, child.HeadingRadians, 200, child.Speed, child.Metabolism,
            child.SensingRadius, child.SteeringRate, child.Brain, child.Species, child.ParentId, child.AttackRadius, child.AttackEfficiency);
        var resumed = new Simulation(settings, new WorldState(100, 80, [fundedChild], []), initial.TickCount, nextOrganismId: initial.NextOrganismId);
        resumed.AdvanceTick(); var grandchild = Assert.Single(resumed.State.World.Organisms, o => o.ParentId == child.Id);
        Assert.Equal(123, grandchild.AttackRadius); Assert.Equal(0.02, grandchild.AttackEfficiency);
        Assert.Equal(7, resumed.State.World.Organisms.Single(o => o.Id == child.Id).ParentId);
    }

    [Theory]
    [InlineData(Species.Herbivore)]
    [InlineData(Species.Predator)]
    public void MovementDrainReflectionFeedingAndReproductionPreserveAttackTraitsAndAncestry(Species species)
    {
        var actor = Organism(7, 1000, 0.25, species, parent: 999, x: 99, speed: 2, metabolism: 1);
        var organisms = species == Species.Herbivore ? new[] { actor } : new[] { actor, Organism(43, energy: 20, x: 99) };
        var settings = PredationFixture.Settings(herbivores: 1, predators: species == Species.Predator ? 1 : 0, food: 1, cap: 4);
        var simulation = new Simulation(settings, new WorldState(100, 80, organisms, [new Food(0, 99, 40, 20)]));
        simulation.AdvanceTick(); var adult = simulation.State.World.Organisms.Single(o => o.Id == 7);
        Assert.Equal(99, adult.X); Assert.Equal(Math.PI, adult.HeadingRadians); Assert.Equal(999, adult.ParentId);
        Assert.Equal(1000, adult.AttackRadius); Assert.Equal(0.25, adult.AttackEfficiency);
        var child = Assert.Single(simulation.State.World.Organisms, o => o.ParentId == 7);
        Assert.Equal(1000, child.AttackRadius); Assert.Equal(0.25, child.AttackEfficiency);
        Assert.Equal(species == Species.Herbivore ? 169 : 165, adult.Energy);
    }

    [Theory]
    [InlineData(0.0, 1000.0, 1.0, false)]
    [InlineData(8.0, 0.0, 0.0, true)]
    public void AttacksStillUseOnlyLegacyRadiusAndLegacyTransferRatio(double pickup, double radius, double efficiency, bool attacks)
    {
        var predator = Organism(7, radius, efficiency, Species.Predator, energy: 100);
        var simulation = new Simulation(PredationFixture.Settings(herbivores: 1, predators: 1, radius: pickup),
            new WorldState(100, 80, [predator, Organism(43, energy: 75)], []));
        simulation.AdvanceTick(); var adult = simulation.State.World.Organisms[0];
        Assert.Equal(attacks ? 160 : 100, adult.Energy); Assert.Equal(attacks ? 1 : 2, simulation.State.World.Organisms.Count);
        Assert.Equal(attacks ? 1 : 0, simulation.GetStatistics().TotalPredationEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedBirthRollsBackExactAttackTraitsAndRetry(int failure)
    {
        Simulation Create()
        {
            var o = Organism(radius: double.MaxValue, efficiency: 0.02, parent: 999);
            return new(AttackSettings.Copy(PredationFixture.Settings(herbivores: 1, predators: 0, cap: 2, neural: true, mutation: 0.1)),
                new WorldState(100, 80, [o], []));
        }
        var simulation = Create(); var reference = Create(); var state = simulation.State; var random = simulation.RandomState;
        var id = simulation.NextOrganismId;
        simulation.FailBeforeMutationForTesting = failure == 0; simulation.FailWorldConstructionForTesting = failure == 1;
        simulation.FailOffspringTraitConstructionForTesting = failure == 2;
        Assert.ThrowsAny<Exception>(simulation.AdvanceTick); Assert.Same(state, simulation.State); Assert.Equal(random, simulation.RandomState); Assert.Equal(id, simulation.NextOrganismId);
        Assert.Equal(double.MaxValue, Assert.Single(simulation.State.World.Organisms).AttackRadius);
        simulation.FailBeforeMutationForTesting = simulation.FailWorldConstructionForTesting = simulation.FailOffspringTraitConstructionForTesting = false;
        simulation.AdvanceTick(); reference.AdvanceTick(); PredationFixture.Same(reference, simulation);
        Assert.All(simulation.State.World.Organisms, o => { Assert.Equal(double.MaxValue, o.AttackRadius); Assert.Equal(0.02, o.AttackEfficiency); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnabledInitialVariationProducesIdenticalFullPostTaskRunsForFiveThousandFiveHundredTicks(bool neural)
    {
        var settings = AttackSettings.Copy(ExperimentSettings.Create(new(neural ? ControllerMode.Neural : ControllerMode.RuleBased, 0.1, 5)),
            efficiency: 0.5, initialRadius: 4, initialEfficiency: 0.2);
        var first = new Simulation(settings); var second = new Simulation(settings);
        first.EnableDiagnosticsFor(36); second.EnableDiagnosticsFor(36); first.CaptureResearchTrace = second.CaptureResearchTrace = true;
        for (var tick = 0; tick <= 5500; tick++)
        {
            PredationFixture.Same(first, second); Assert.Equal(first.LastResearchSample, second.LastResearchSample);
            Assert.Equal(first.State.World.Organisms.Select(o => (o.Id, o.ParentId, o.AttackRadius, o.AttackEfficiency)),
                second.State.World.Organisms.Select(o => (o.Id, o.ParentId, o.AttackRadius, o.AttackEfficiency)));
            if (tick != 5500) { first.AdvanceTick(); second.AdvanceTick(); }
        }
    }

    internal static string[] ProjectLegacy(ControllerMode mode, int ticks)
    {
        var simulation = new Simulation(ExperimentSettings.Create(new(mode, 0.1, 5)));
        simulation.EnableDiagnosticsFor(36); simulation.CaptureResearchTrace = true;
        var history = new PopulationHistory(simulation); var lineage = new LineageLedger(simulation);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var tick = 0; tick <= ticks; tick++)
        {
            if (tick != 0) { simulation.AdvanceTick(); history.Observe(simulation); lineage.Observe(simulation); }
            Assert.Equal(simulation.Settings.WorldWidth, simulation.State.World.WorldWidth);
            Assert.Equal(simulation.Settings.WorldHeight, simulation.State.World.WorldHeight);
            stream.Position = 0; stream.SetLength(0);
            writer.Write(simulation.TickCount); writer.Write(simulation.RandomState.State); writer.Write(simulation.RandomState.Increment);
            writer.Write(simulation.NextFoodId); writer.Write(simulation.NextOrganismId);
            writer.Write(simulation.State.World.Organisms.Count);
            foreach (var o in simulation.State.World.Organisms)
            {
                writer.Write(o.Id); writer.Write((int)o.Species); WriteValue(writer, o.ParentId);
                foreach (var value in new[] { o.X, o.Y, o.HeadingRadians, o.Energy, o.Speed, o.Metabolism, o.SensingRadius, o.SteeringRate }) writer.Write(value);
                foreach (var weight in o.Brain.Weights) writer.Write(weight);
            }
            writer.Write(simulation.State.World.FoodItems.Count);
            foreach (var f in simulation.State.World.FoodItems) { writer.Write(f.Id); writer.Write(f.X); writer.Write(f.Y); writer.Write(f.Energy); }
            var statistics = simulation.GetStatistics();
            foreach (var p in typeof(PopulationStatistics).GetProperties().Where(p => p.Name is not "AttackRadius" and not "AttackEfficiency").OrderBy(p => p.Name, StringComparer.Ordinal))
                WriteValue(writer, p.GetValue(statistics));
            WriteValue(writer, simulation.LastDiagnosticSample); WriteValue(writer, simulation.LastResearchSample);
            writer.Flush(); hash.AppendData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
        }
        var snapshot = history.Capture();
        // Normalize only version provenance, which legitimately changes on a commit.
        snapshot = snapshot with { Metadata = snapshot.Metadata with { CoreVersion = "projection-reference", RuntimeVersion = "net10" } };
        using var populationCsv = new StringWriter(); PopulationHistoryCsv.Write(populationCsv, snapshot);
        using var lineageCsv = new StringWriter(); LineageCsv.Write(lineageCsv, lineage.Capture());
        return [Convert.ToHexString(hash.GetHashAndReset()),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(populationCsv.ToString()))),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lineageCsv.ToString())))];
    }

    private static void WriteValue(BinaryWriter writer, object? value)
    {
        writer.Write(value is not null); if (value is null) return;
        switch (value)
        {
            case double d: writer.Write(d); break;
            case int i: writer.Write(i); break;
            case long l: writer.Write(l); break;
            case bool b: writer.Write(b); break;
            case string s: writer.Write(s); break;
            case Enum e: writer.Write(Convert.ToInt32(e)); break;
            default:
                foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.Name != "EqualityContract" && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal))
                    WriteValue(writer, property.GetValue(value));
                break;
        }
    }
}
