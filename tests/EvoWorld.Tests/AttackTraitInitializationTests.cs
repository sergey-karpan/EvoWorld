using EvoWorld.Core;
using EvoWorld.Core.Random;
using Xunit;

namespace EvoWorld.Tests;

public sealed class AttackTraitInitializationTests
{
    public static IEnumerable<object[]> PhaseModes()
    {
        foreach (var task8 in new[] { false, true })
            foreach (var task9 in new[] { false, true })
                foreach (var task11 in new[] { false, true })
                    foreach (var attack in new[] { false, true }) yield return [task8, task9, task11, attack];
    }

    [Theory]
    [MemberData(nameof(PhaseModes))]
    public void AllSixteenModesKeepStrictPhaseOrderAndExactDrawCounts(bool task8, bool task9, bool task11, bool attack)
    {
        const int n = 3, f = 2;
        var legacy = PredatorFixture.Settings(herbivores: 2, predators: 1, food: f, oldVariation: task8, newVariation: task9, neural: task11);
        var settings = AttackSettings.Copy(legacy, efficiency: 0.5, initialRadius: attack ? 4 : 0, initialEfficiency: attack ? 0.2 : 0);
        var random = new Pcg32(settings.Seed); var oldRandom = new Pcg32(settings.Seed);
        var actual = WorldInitializer.Create(settings, random); var oldWorld = WorldInitializer.Create(legacy, oldRandom);
        var reference = new Pcg32(settings.Seed);
        var oldDraws = 3 * n + 2 * f + (task8 ? 2 * n : 0) + (task9 ? 2 * n : 0) + (task11 ? 74 * n : 0);
        for (var i = 0; i < oldDraws; i++) reference.NextUInt();
        Assert.Equal(oldRandom.CaptureState(), reference.CaptureState());
        for (var id = 0; id < n; id++)
        {
            var before = oldWorld.Organisms[id]; var after = actual.Organisms[id];
            Assert.Equal(new[] { before.X, before.Y, before.HeadingRadians, before.Energy, before.Speed, before.Metabolism, before.SensingRadius, before.SteeringRate },
                new[] { after.X, after.Y, after.HeadingRadians, after.Energy, after.Speed, after.Metabolism, after.SensingRadius, after.SteeringRate });
            Assert.Equal(before.Species, after.Species); Assert.Equal(before.ParentId, after.ParentId);
            Assert.Equal(before.Brain.Weights.ToArray(), after.Brain.Weights.ToArray());
            var radius = attack ? Math.Clamp(8 + (2 * (reference.NextUInt() / 4294967296.0) - 1) * 4, 0, 50) : 8;
            var efficiency = attack ? Math.Clamp(0.5 + (2 * (reference.NextUInt() / 4294967296.0) - 1) * 0.2, 0, 1) : 0.5;
            Assert.Equal(radius, after.AttackRadius); Assert.Equal(efficiency, after.AttackEfficiency);
        }
        Assert.Equal(reference.CaptureState(), random.CaptureState());
        Assert.Equal(oldWorld.FoodItems.Select(x => (x.Id, x.X, x.Y, x.Energy)), actual.FoodItems.Select(x => (x.Id, x.X, x.Y, x.Energy)));
    }

    [Fact]
    public void IndependentOfficialPcgScalarFixtureChecksTraitDrawOrderAfterPlacement()
    {
        // Seed 42 / stream 1: the official C-reference sequence already documented
        // by historical Pcg32Tests starts 4df1ccf9,e5838752,58ed9e10,f3e37b51,e7664374.
        // With N=1,F=0, three placement draws precede radius then efficiency.
        var legacy = PredatorFixture.Settings(herbivores: 1, predators: 0, food: 0, initialNeural: 0, neural: false);
        var settings = AttackSettings.Copy(legacy, radius: 8, efficiency: 0.5, initialRadius: 4, initialEfficiency: 0.25);
        var o = Assert.Single(WorldInitializer.Create(settings).Organisms);
        var expectedRadius = 8 + (2 * (0xf3e37b51U / 4294967296.0) - 1) * 4;
        var expectedEfficiency = 0.5 + (2 * (0xe7664374U / 4294967296.0) - 1) * 0.25;
        Assert.Equal(BitConverter.DoubleToInt64Bits(expectedRadius), BitConverter.DoubleToInt64Bits(o.AttackRadius));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expectedEfficiency), BitConverter.DoubleToInt64Bits(o.AttackEfficiency));
    }

    [Theory]
    [InlineData(0.0, 0.2)]
    [InlineData(4.0, 0.0)]
    public void OneNonzeroAmplitudeStillConsumesBothDrawsPerOrganism(double radiusAmplitude, double efficiencyAmplitude)
    {
        var legacy = PredatorFixture.Settings(herbivores: 1, predators: 1, food: 0, neural: false);
        var settings = AttackSettings.Copy(legacy, efficiency: 0.5, initialRadius: radiusAmplitude, initialEfficiency: efficiencyAmplitude);
        var generator = new Pcg32(42); var world = WorldInitializer.Create(settings, generator); var expected = new Pcg32(42);
        for (var i = 0; i < 6; i++) expected.NextUInt();
        foreach (var organism in world.Organisms)
        {
            var radiusDraw = expected.NextUInt(); var efficiencyDraw = expected.NextUInt();
            var radius = 8 + (2 * (radiusDraw / 4294967296.0) - 1) * radiusAmplitude;
            var efficiency = 0.5 + (2 * (efficiencyDraw / 4294967296.0) - 1) * efficiencyAmplitude;
            Assert.Equal(radius, organism.AttackRadius); Assert.Equal(efficiency, organism.AttackEfficiency);
            if (radiusAmplitude == 0) Assert.Equal(8, organism.AttackRadius);
            if (efficiencyAmplitude == 0) Assert.Equal(0.5, organism.AttackEfficiency);
        }
        Assert.Equal(expected.CaptureState(), generator.CaptureState());
    }

    [Fact]
    public void DisabledPhaseHasNoDrawsDespiteReservedMutationDefaultsAndAssignsNonDefaultBaselines()
    {
        var legacy = PredatorFixture.Settings(herbivores: 2, predators: 1, food: 2, neural: true);
        var settings = AttackSettings.Copy(legacy, radius: 17, efficiency: 0.3, radiusMutation: double.MaxValue, efficiencyMutation: double.MaxValue);
        var expected = new Pcg32(42); var actual = new Pcg32(42);
        WorldInitializer.Create(legacy, expected); var world = WorldInitializer.Create(settings, actual);
        Assert.Equal(expected.CaptureState(), actual.CaptureState());
        Assert.All(world.Organisms, o => { Assert.Equal(17, o.AttackRadius); Assert.Equal(0.3, o.AttackEfficiency); });
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(2147483648U)]
    [InlineData(uint.MaxValue)]
    public void ExtremeFiniteAmplitudesSaturateSafelyForRadiusAndEfficiency(uint draw)
    {
        var radius = WorldInitializer.VaryAttackTrait(double.MaxValue / 2, double.MaxValue, 0, double.MaxValue, draw);
        var efficiency = WorldInitializer.VaryAttackTrait(0.5, double.MaxValue, 0, 1, draw);
        Assert.True(double.IsFinite(radius)); Assert.InRange(radius, 0, double.MaxValue);
        Assert.True(double.IsFinite(efficiency)); Assert.InRange(efficiency, 0, 1);
        if (draw == 0) { Assert.Equal(0, radius); Assert.Equal(0, efficiency); }
        else if (draw == uint.MaxValue) { Assert.Equal(double.MaxValue, radius); Assert.Equal(1, efficiency); }
        else { Assert.Equal(double.MaxValue / 2, radius); Assert.Equal(0.5, efficiency); }
    }

    [Fact]
    public void ZeroAmplitudeAndSubnormalVariationHaveExactBoundarySemantics()
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(WorldInitializer.VaryAttackTrait(-0.0, 0, 0, 50, uint.MaxValue)));
        Assert.Equal(double.MaxValue, WorldInitializer.VaryAttackTrait(double.MaxValue, 0, 0, double.MaxValue, 0));
        Assert.Equal(0, WorldInitializer.VaryAttackTrait(double.Epsilon, double.Epsilon, 0, 1, 0));
        Assert.Equal(double.Epsilon, WorldInitializer.VaryAttackTrait(double.Epsilon, double.Epsilon, 0, 1, 2147483648));
    }

    [Fact]
    public void ProductionExtremeInitialValuesStayFiniteAndWithinBoundsForBothSpecies()
    {
        var settings = AttackSettings.Copy(PredatorFixture.Settings(herbivores: 20, predators: 5, food: 0),
            radius: double.MaxValue / 2, maxRadius: double.MaxValue, efficiency: 0.5, initialRadius: double.MaxValue, initialEfficiency: double.MaxValue);
        Assert.All(WorldInitializer.Create(settings).Organisms, o =>
        {
            Assert.True(double.IsFinite(o.AttackRadius)); Assert.InRange(o.AttackRadius, 0, double.MaxValue);
            Assert.True(double.IsFinite(o.AttackEfficiency)); Assert.InRange(o.AttackEfficiency, 0, 1);
        });
    }

    [Fact]
    public void EmptyPopulationActivePhaseConsumesNoAdditionalDraws()
    {
        var legacy = PredatorFixture.Settings(herbivores: 0, predators: 0, food: 3);
        var settings = AttackSettings.Copy(legacy, initialRadius: 1, initialEfficiency: 0.1);
        var actual = new Pcg32(42); var expected = new Pcg32(42);
        Assert.Empty(WorldInitializer.Create(settings, actual).Organisms); for (var i = 0; i < 6; i++) expected.NextUInt();
        Assert.Equal(expected.CaptureState(), actual.CaptureState());
    }

    [Fact]
    public void ActiveOtherTraitDoesNotChangeNegativeZeroBaselineBits()
    {
        var settings = AttackSettings.Copy(PredatorFixture.Settings(herbivores: 1, predators: 1, food: 0),
            radius: -0.0, efficiency: 0.5, initialEfficiency: 0.2);
        Assert.All(WorldInitializer.Create(settings).Organisms, o =>
            Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(o.AttackRadius)));
    }
}
