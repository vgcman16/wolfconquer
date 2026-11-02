using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFusePreparationTests
{
    private static PlayerItemSnapshotItem Item(uint uid, uint itemId = 4_100_001, byte bound = 0,
        uint word = 100_003, IReadOnlyList<PlayerItemSnapshotItem>? deposits = null) => new()
    {
        Uid = uid, ItemId = itemId, StackSize = 1, Bound = bound,
        Attribute1 = word, Attribute2 = word, Attribute3 = word, Attribute4 = word, Attribute5 = word,
        DepositedItems = deposits ?? [],
    };

    private static PlayerItemSnapshot Owner(uint word = 100_003) =>
        PlayerItemSnapshot.Empty with { InventoryItems = new[] { Item(1, word: word), Item(2, 4_100_002, bound: 1) } };

    private static XuanbaoComposeAttributeLimit[] Caps(int maximum = 400) => Enumerable.Range(1, 13)
        .Select(type => XuanbaoParser.ParseComposeAttributeLimitRow("fixture://relic-compose", type,
            FormattableString.Invariant($"{type}@@0@@1@@{maximum}@@"))).ToArray();

    private static WorldDataCatalog World(IReadOnlyList<XuanbaoComposeAttributeLimit>? caps = null)
    {
        var rows = LegacyMaxRandomRelicTestData.CreateWorldData();
        return LegacyItemWorldDataFixture.Create(rows.Items, rows.XuanbaoRandomAttributes,
            xuanbaoComposeAttributeLimits: caps ?? Caps());
    }

    private static LegacyRelicFusePreparation Prepare(WorldDataCatalog world, PlayerItemSnapshot owner)
    {
        Assert.True(LegacyRelicFuseInputs.TryCapture(owner, 1, 2, out var inputs, out var reason), reason);
        Assert.True(LegacyRelicFusePreparation.TryPrepare(world, inputs!, out var prepared, out reason), reason);
        return Assert.IsType<LegacyRelicFusePreparation>(prepared);
    }

    private static LegacyRelicFuseRollData Roll(LegacyRelicFusePreparation prepared, PlayerItemSnapshot owner, int seed = 0)
    {
        Assert.True(prepared.TryRoll(owner, new Random(seed), out var roll, out var reason), reason);
        return Assert.IsType<LegacyRelicFuseRollData>(roll);
    }

    private static uint[] Words(LegacyRelicFuseRollData roll) =>
        [roll.Attribute1, roll.Attribute2, roll.Attribute3, roll.Attribute4, roll.Attribute5];

    [Theory]
    [InlineData(0U)]
    [InlineData(100_001U)]
    [InlineData(100_003U)]
    [InlineData(100_114U)]
    public void ActualCrossItemInputsAndEmptySlotsProduceOnlyAllowedBoundedWords(uint inherited)
    {
        var owner = Owner(inherited);
        var world = World();
        var prepared = Prepare(world, owner);
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        for (var seed = 0; seed < 64; seed++)
        {
            var roll = Roll(prepared, owner, seed);
            Assert.Equal(1U, roll.FirstUid); Assert.Equal(2U, roll.SecondUid);
            Assert.Equal(4_100_001U, roll.ItemId); Assert.Equal((byte)1, roll.Bound);
            Assert.InRange(roll.Durability, (ushort)3_000, (ushort)10_000);
            Assert.Equal(0, roll.Durability % 100);
            foreach (var raw in Words(roll))
            {
                var type = (LegacyChiAttributeType)(raw % 100);
                Assert.True(WorldPacketHandlers.IsAllowedLegacyRelicAttribute(roll.ItemId, type));
                Assert.InRange((byte)type, (byte)1, (byte)14);
                if (type == LegacyChiAttributeType.LukyStrike) Assert.Equal(100_114U, raw);
                else
                {
                    Assert.InRange(raw / 1000, 0U, 400U);
                    Assert.Equal(raw / 1000 == 400 ? 100U : 0U, raw % 1000 - (uint)type);
                }
            }
        }
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Assert.Empty(owner.RelicFuseItems);
    }

    [Fact]
    public void LuckyHasItsRealFixedValueWithoutAnUnusedComposeCap()
    {
        var world = World(Caps(30)); // Exactly13 formula-used caps; no row14.
        var owner = Owner(100_114);
        var prepared = Prepare(world, owner);
        var words = Enumerable.Range(0, 32).SelectMany(seed => Words(Roll(prepared, owner, seed))).ToArray();
        Assert.Contains(100_114U, words);
        Assert.All(words.Where(raw => raw % 100 == 14), raw => Assert.Equal(100_114U, raw));
    }

    [Fact]
    public void StaleMutableInputRefusesBeforeTheFirstEntropyDraw()
    {
        var owner = Owner(); var prepared = Prepare(World(), owner);
        owner.InventoryItems[0].OwnerUid = 44;
        var actual = new Random(17); var expected = new Random(17);
        Assert.False(prepared.TryRoll(owner, actual, out var roll, out var reason));
        Assert.Null(roll); Assert.Equal("relicFusePreparedOwnerChanged", reason);
        Assert.Equal(expected.Next(), actual.Next());
    }

    [Theory]
    [InlineData(99U)]
    [InlineData(100_203U)]
    [InlineData(65_536_003U)]
    public void UnknownOrUnrepresentableAttributeWordsRefuseBeforeResultPreparation(uint word)
    {
        var owner = Owner(word);
        Assert.True(LegacyRelicFuseInputs.TryCapture(owner, 1, 2, out var inputs, out _));
        Assert.False(LegacyRelicFusePreparation.TryPrepare(World(), inputs!, out var prepared, out var reason));
        Assert.Null(prepared); Assert.Equal("relicFuseAttributeEncodingInvalid", reason);
    }
}
