using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Metadata-only controls. These pure tables/plans do not establish admission,
// file ownership, a successful journal or permission to mutate a live character.
public sealed class LegacyRuneComposeProgressWitnessTests
{
    [Fact]
    public void ParsedCurveCapturesThePlansExactUsedRowsWithoutExposingItsCapture()
    {
        var rows = Rows();
        var witness = new LegacyRuneComposeProgressWitness(Catalog(rows));
        var plan = Plan(witness);
        Assert.Equal(4_020_518U, plan.Target.ItemId);
        Assert.Equal(0U, plan.Target.RuneExperience);
        Assert.Equal(702U, Assert.Single(plan.Consumed).Uid);
        LegacyRuneComposeProgressRow[] expected =
        [new(402, 17, 94000), new(402, 18, 109000), new(402, 19, 131500), new(404, 1, 10)];
        var captured = witness.CaptureUsed(plan.UsedProgress);
        Assert.Equal(expected, captured);
        captured[0] = new(402, 17, 1);
        Assert.Equal(expected, witness.CaptureUsed(plan.UsedProgress));
        Assert.True(witness.IsCurrent());
    }

    [Fact]
    public void UsedRawFieldDriftInvalidatesTheCapturedCurveDespiteUnchangedTypedValues()
    {
        var rows = Rows();
        var witness = new LegacyRuneComposeProgressWitness(Catalog(rows));
        var plan = Plan(witness);
        witness.CaptureUsed(plan.UsedProgress);
        Assert.Equal(94000L, rows[0].Experience);
        Assert.IsType<string[]>(rows[0].Fields)[3] = "94001";
        Assert.Equal(94000L, rows[0].Experience);
        Assert.False(witness.IsCurrent());
        Assert.Throws<InvalidOperationException>(() => witness.CaptureUsed(plan.UsedProgress));
    }

    [Fact]
    public void InconsistentRawAndTypedProgressCannotAcquireAUsedRowCapture()
    {
        var rows = Rows();
        Assert.IsType<string[]>(rows[0].Fields)[3] = "94001";
        var witness = new LegacyRuneComposeProgressWitness(Catalog(rows));
        var plan = Plan(witness); // Arithmetic alone is not metadata authority.
        Assert.Throws<InvalidOperationException>(() => witness.CaptureUsed(plan.UsedProgress));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingTheRawOrIndexedRowCannotRefreshAnExistingCapture(bool indexOnly)
    {
        var rows = Rows();
        var catalog = Catalog(rows);
        var witness = new LegacyRuneComposeProgressWitness(catalog);
        var plan = Plan(witness);
        witness.CaptureUsed(plan.UsedProgress);
        var equalReplacement = Row("18@@402@@17@@94000@@");
        Assert.Equal(rows[0].Experience, equalReplacement.Experience);
        Assert.NotSame(rows[0], equalReplacement);
        if (indexOnly)
            Assert.IsType<Dictionary<(int RuneType, int Level), RuneLevelExperience>>(
                catalog.RuneLevelExperienceByRuneTypeAndLevel)[(402, 17)] = equalReplacement;
        else rows[0] = equalReplacement;
        Assert.False(witness.IsCurrent());
        Assert.Throws<InvalidOperationException>(() => witness.CaptureUsed(plan.UsedProgress));
    }

    [Fact]
    public void AFirstWinsCatalogIndexDoesNotAuthorizeAnAmbiguousRequiredCurve()
    {
        var rows = Rows();
        rows.Add(Row("118@@402@@17@@94001@@"));
        var catalog = Catalog(rows);
        Assert.Same(rows[0], catalog.RuneLevelExperienceByRuneTypeAndLevel[(402, 17)]);
        var witness = new LegacyRuneComposeProgressWitness(catalog);
        Assert.False(witness.Lookup.ContainsKey((402, 17)));
        Assert.Throws<InvalidOperationException>(() => Plan(witness));
    }

    [Fact]
    public void AnUnrelatedAmbiguousCurveCannotSupplyOrBlockTheUsedProgress()
    {
        var rows = Rows();
        rows.Add(Row("998@@410@@99@@100@@"));
        rows.Add(Row("999@@410@@99@@200@@"));
        var witness = new LegacyRuneComposeProgressWitness(Catalog(rows));
        var plan = Plan(witness);
        Assert.False(witness.Lookup.ContainsKey((410, 99)));
        var captured = witness.CaptureUsed(plan.UsedProgress);
        Assert.Equal(4, captured.Length);
        Assert.DoesNotContain(captured, row => row.Family == 410);
        Assert.Equal(4_020_518U, plan.Target.ItemId);
        Assert.True(witness.IsCurrent());
    }

    private static LegacyRuneComposePlan Plan(LegacyRuneComposeProgressWitness witness)
    {
        var target = new PlayerItemSnapshotItem
        { Uid = 701, ItemId = 4_020_517, Position = 212, StackSize = 1 };
        var essence = new PlayerItemSnapshotItem
        { Uid = 702, ItemId = 4_040_001, Position = 0, StackSize = 1500 };
        return WorldPacketHandlers.PlanRecordedRuneCompose(new([essence], [], [], [], [target]),
            target.Uid, [essence.Uid], witness.Lookup,
            new Dictionary<uint, LegacyRuneComposeMaterialMetadata>
            { [essence.ItemId] = new(essence.ItemId, 10000) });
    }

    private static WorldDataCatalog Catalog(List<RuneLevelExperience> rows) =>
        NativeCallerWorldDataFixture.Create(Array.Empty<ItemType>(),
            Array.Empty<XuanbaoRandomAttribute>(), runeLevelExperience: rows);

    private static List<RuneLevelExperience> Rows() => new()
    {
        Row("18@@402@@17@@94000@@"), Row("19@@402@@18@@109000@@"),
        Row("20@@402@@19@@131500@@"), Row("38@@404@@1@@10@@"),
    };

    private static RuneLevelExperience Row(string text) =>
        RuneParser.ParseLevelExperienceRow("owned-progress-rune_levexp.txt", 1, text);
}
