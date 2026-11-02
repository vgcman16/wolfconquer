using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoRuntimeRulesTests
{
    private static WorldDataCatalog World()
    {
        var items = LegacyMaxRandomRelicTestData.CreateWorldData();
        var compose = items.XuanbaoRandomAttributes.Select(row => row.AttributeType).Distinct()
            .Select(type => XuanbaoParser.ParseComposeAttributeLimitRow("fixture://compose", 1,
                FormattableString.Invariant($"{type}@@0@@1@@1000@@"))).ToArray();
        return LegacyItemWorldDataFixture.Create(items.Items, items.XuanbaoRandomAttributes,
            xuanbaoComposeAttributeLimits: compose);
    }

    private static (byte[] Compose, byte[] Random) Packed(WorldDataCatalog world,
        Func<XuanbaoComposeAttributeLimit, int[]>? composeValues = null,
        Func<XuanbaoRandomAttribute, (int Min, int Max)>? randomValues = null, bool reverse = true)
    {
        var compose = new StringBuilder();
        foreach (var row in world.XuanbaoComposeAttributeLimits)
        {
            var values = composeValues?.Invoke(row) ?? row.Values.ToArray();
            compose.Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append("@@")
                .Append(string.Join("@@", values.Select(value => value.ToString(CultureInfo.InvariantCulture)))).Append("@@\r\n");
        }
        var random = new StringBuilder();
        var rows = reverse ? world.XuanbaoRandomAttributes.Reverse() : world.XuanbaoRandomAttributes;
        foreach (var row in rows)
        {
            var values = randomValues?.Invoke(row) ?? (row.MinValue, row.MaxValue);
            random.Append(row.ItemTypeId.ToString("D10", CultureInfo.InvariantCulture)).Append("@@")
                .Append(row.AttributeType.ToString("D3", CultureInfo.InvariantCulture)).Append("@@")
                .Append(values.Item1.ToString("D10", CultureInfo.InvariantCulture)).Append("@@")
                .Append(values.Item2.ToString("D10", CultureInfo.InvariantCulture)).Append("@@\r\n");
        }
        return (ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes(compose.ToString())),
            ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes(random.ToString())));
    }

    private static Chinese7937XuanbaoRuleCatalogs Source((byte[] Compose, byte[] Random) packed)
    {
        Assert.True(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, packed.Compose, packed.Random,
            SHA256.HashData(packed.Compose), SHA256.HashData(packed.Random), out var source, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoRuleCatalogs>(source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceOrderIsRetainedButRealGeneratorCandidateOrderStaysServerOwned(bool maximum)
    {
        var world = World();
        var baseline = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        var serverOrder = world.XuanbaoRandomAttributesByItemTypeId[4_100_001].Select(row => row.AttributeType).ToArray();
        var source = Source(Packed(world));
        var sourceOrder = source.RandomAttributes.Where(row => row.ItemTypeId == 4_100_001).Select(row => row.AttributeType).ToArray();
        Assert.False(serverOrder.SequenceEqual(sourceOrder));
        Assert.True(world.TryBindChinese7937XuanbaoRules(source, out var reason), reason);
        var runtime = Assert.IsType<Chinese7937XuanbaoRuntimeRules>(world.Selected7937XuanbaoRules);
        Assert.Same(source, runtime.Source);
        Assert.Equal(serverOrder, runtime.RandomAttributesByItemTypeId[4_100_001].Select(row => row.AttributeType));
        Assert.Equal(sourceOrder, source.RandomAttributes.Where(row => row.ItemTypeId == 4_100_001).Select(row => row.AttributeType));
        var selected = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        Assert.Same(runtime.RandomAttributesByItemTypeId, selected.RelicRandomAttributesByItemTypeId);
        var rolls = new LegacyBossSpecialDeathDropRolls(DeityLandRelicItemIndex: 0,
            DeityLandRelicAttributeCount: 5, DeityLandRelicDurabilityRoll: 10,
            DeityLandRelicAttributeIndexes: [0, 0, 0, 0, 0], DeityLandRelicAttributeValues: [100, 100, 100, 100, 100]);
        LegacyBossSpecialGeneratedDropItem oldItem, newItem;
        var oldFound = maximum
            ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(baseline, rolls, out oldItem, out reason)
            : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(baseline, rolls, out oldItem, out reason);
        Assert.True(oldFound, reason);
        var newFound = maximum
            ? LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(selected, rolls, out newItem, out reason)
            : LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(selected, rolls, out newItem, out reason);
        Assert.True(newFound, reason);
        Assert.Equal(oldItem, newItem);
        if (maximum)
            Assert.Equal(LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes,
                new uint[] { newItem.Attribute1, newItem.Attribute2, newItem.Attribute3, newItem.Attribute4, newItem.Attribute5 });
    }

    [Fact]
    public void EffectiveComposeLookupReturnsTheSelectedImmutableRowWithoutGuessingOtherValues()
    {
        var world = World();
        var source = Source(Packed(world));
        Assert.True(world.TryBindChinese7937XuanbaoRules(source, out var reason), reason);
        Assert.True(world.TryGetEffectiveXuanbaoComposeLimit(3, out var row));
        Assert.Same(source.ComposeLimits.Single(candidate => candidate.Id == 3), row);
        Assert.NotSame(world.XuanbaoComposeAttributeLimitsById[3], row);
        Assert.Equal(new[] { 0, 1, 1000 }, row!.Values);
        Assert.False(world.TryGetEffectiveXuanbaoComposeLimit(99, out _));
        Assert.Throws<NotSupportedException>(() => ((IList<int>)row.Values).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<XuanbaoRandomAttribute>)world.Selected7937XuanbaoRules!
            .RandomAttributesByItemTypeId[4_100_001]).Clear());
    }

    [Fact]
    public void NumericComposeConflictRefusesBeforePublicationAndPreservesUnselectedBehavior()
    {
        var world = World();
        var source = Source(Packed(world, composeValues: row => [row.Values[0], row.Values[1], row.Values[2] + 1]));
        Assert.False(world.TryBindChinese7937XuanbaoRules(source, out var reason));
        Assert.Equal("selected7937ComposeOwnerOrValuesConflict", reason);
        Assert.Null(world.Selected7937XuanbaoRules);
        Assert.True(world.TryGetEffectiveXuanbaoComposeLimit(3, out var existing));
        Assert.Same(world.XuanbaoComposeAttributeLimitsById[3], existing);
    }

    [Fact]
    public void AConflictingDuplicateInTheCompleteRawComposeTailCannotHideBehindTheIndex()
    {
        var complete = World();
        var source = Source(Packed(complete));
        var duplicate = complete.XuanbaoComposeAttributeLimits[0] with { Values = new[] { 0, 1, 1001 } };
        WorldDataCatalog malformed;
        try
        {
            malformed = LegacyItemWorldDataFixture.Create(complete.Items, complete.XuanbaoRandomAttributes,
                xuanbaoComposeAttributeLimits: complete.XuanbaoComposeAttributeLimits.Concat([duplicate]).ToArray());
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException)
        {
            // Earlier complete factory rejection also satisfies the invariant;
            // never require the historical first-winner index defect to remain.
            return;
        }
        Assert.False(malformed.TryBindChinese7937XuanbaoRules(source, out var reason));
        Assert.Equal("selected7937ComposeOwnerOrValuesConflict", reason);
        Assert.Null(malformed.Selected7937XuanbaoRules);
    }

    [Fact]
    public void NumericRandomConflictRefusesBeforeAnySelectedStateEscapes()
    {
        var world = World();
        var source = Source(Packed(world, randomValues: row => (row.MinValue, row.MaxValue + 100)));
        Assert.False(world.TryBindChinese7937XuanbaoRules(source, out var reason));
        Assert.Equal("selected7937RandomOwnerOrValuesConflict", reason);
        Assert.Null(world.Selected7937XuanbaoRules);
        Assert.Same(world.XuanbaoRandomAttributesByItemTypeId, world.EffectiveXuanbaoRandomAttributesByItemTypeId);
    }

    [Fact]
    public void MissingActualItemAndComposeOwnersCannotBeReplacedByPlausibleClientRows()
    {
        var complete = World();
        var source = Source(Packed(complete));
        var missingItem = LegacyItemWorldDataFixture.Create(complete.Items.Take(4).ToArray(), complete.XuanbaoRandomAttributes,
            xuanbaoComposeAttributeLimits: complete.XuanbaoComposeAttributeLimits);
        Assert.False(missingItem.TryBindChinese7937XuanbaoRules(source, out var reason));
        Assert.Equal("selected7937RandomOwnerOrValuesConflict", reason);
        Assert.Null(missingItem.Selected7937XuanbaoRules);
        var missingCompose = LegacyMaxRandomRelicTestData.CreateWorldData();
        Assert.False(missingCompose.TryBindChinese7937XuanbaoRules(source, out reason));
        Assert.Equal("selected7937ComposeOwnerOrValuesConflict", reason);
        Assert.Null(missingCompose.Selected7937XuanbaoRules);
    }

    [Fact]
    public void OnceBoundRuntimeRefusesReplacementAndRetainsEarlierDeepOwnedMetadata()
    {
        var world = World();
        var packed = Packed(world);
        var first = Source(packed);
        Assert.True(world.TryBindChinese7937XuanbaoRules(first, out _));
        var bound = world.Selected7937XuanbaoRules;
        Assert.True(world.TryBindChinese7937XuanbaoRules(first, out _));
        Assert.False(world.TryBindChinese7937XuanbaoRules(Source(packed), out var reason));
        Assert.Equal("selected7937RuleCatalogAlreadyBound", reason);
        Assert.Same(bound, world.Selected7937XuanbaoRules);
        Array.Clear(packed.Compose); Array.Clear(packed.Random);
        Assert.Equal(1000, bound!.Source.ComposeLimits[0].Values[2]);
        Assert.Equal("0004100005", bound.Source.RandomAttributes[0].Fields[0]);
    }

    [Fact]
    public void BoundRowsNeverRetainMutableSeedValueFieldOrListAliases()
    {
        var original = World();
        var seedValues = original.XuanbaoComposeAttributeLimits.Select(row => row.Values.ToArray()).ToArray();
        var compose = original.XuanbaoComposeAttributeLimits.Select((row, index) => row with
            { Values = seedValues[index], Fields = row.Fields.ToArray() }).ToList();
        var random = original.XuanbaoRandomAttributes.Select(row => row with { Fields = row.Fields.ToArray() }).ToList();
        var world = LegacyItemWorldDataFixture.Create(original.Items, random, xuanbaoComposeAttributeLimits: compose);
        var source = Source(Packed(world));
        Assert.True(world.TryBindChinese7937XuanbaoRules(source, out var reason), reason);
        Array.Fill(seedValues[0], 9999);
        Array.Fill((string[])compose[0].Fields, "mutated");
        Array.Fill((string[])random[0].Fields, "mutated");
        compose.Clear(); random.Clear();
        Assert.True(world.TryGetEffectiveXuanbaoComposeLimit(3, out var cap));
        Assert.Equal(new[] { 0, 1, 1000 }, cap!.Values);
        Assert.DoesNotContain("mutated", cap.Fields);
        var catalog = LegacyBossSpecialDeathDropCatalog.FromWorldData(world);
        Assert.DoesNotContain("mutated", catalog.RelicRandomAttributesByItemTypeId[4_100_001][0].Fields);
        Assert.True(LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(catalog,
            new LegacyBossSpecialDeathDropRolls(DeityLandRelicItemIndex: 0,
                DeityLandRelicAttributeIndexes: [0, 0, 0, 0, 0]), out var item, out reason), reason);
        Assert.Equal(LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes,
            new uint[] { item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5 });
    }
}
