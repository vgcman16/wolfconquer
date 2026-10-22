using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSalePersistenceTests
{
    [Theory]
    [InlineData("utf8", "\n")]
    [InlineData("utf8-bom", "\r\n")]
    [InlineData("windows1252", "\r")]
    public void OneUnitStackPlanPersistsOnlyQuantityAndMoneyWhileKeepingAllMetadata(string encodingName, string newline)
    {
        var encoding = EncodingFor(encodingName);
        var original = WriteRichStack(encodingName, newline);
        var beforeItems = File.ReadAllBytes(ItemPath);
        var expectedItems = WithFirstStackQuantity(beforeItems, 2);
        var expectedCharacter = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out _)
            .Replace("Money=1000" + newline, "Money=1030" + newline, StringComparison.Ordinal);
        var updated = OneUnitStackResult(original);

        Assert.True(LegacyNpcSalePersistence.TryPersist(original, updated, Items, out var reason), reason);

        // The byte oracle is independent of WithStackSize/serialization: only the native
        // DBItem quantity field changes, including all other containers/perfection records.
        var actualItems = File.ReadAllBytes(ItemPath);
        Assert.Equal(expectedItems, actualItems);
        Assert.Equal(Encode(expectedCharacter, encoding), File.ReadAllBytes(CharacterPath));
        var remaining = Assert.Single(PlayerItemSnapshotLoader.Parse(actualItems).InventoryItems, item => item.Uid == 10);
        Assert.Equal((ushort)2, remaining.StackSize);
        Assert.Equal((ushort)3, Assert.Single(original.PlayerItems.InventoryItems, item => item.Uid == 10).StackSize);
        Assert.Equal(23u, remaining.PerfectionLevel);
        Assert.Equal("StackOwner", remaining.OwnerName);
        Assert.Equal("StackSignature", remaining.Signature);
        Assert.Equal(995u, remaining.Attribute5);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)4)]
    public void StaleDiskStackCannotBeOverwrittenByAnOlderOneUnitPlan(ushort currentDiskQuantity)
    {
        var original = WriteRichStack("utf8", "\n");
        var updated = OneUnitStackResult(original);
        File.WriteAllBytes(ItemPath, WithFirstStackQuantity(File.ReadAllBytes(ItemPath), currentDiskQuantity));

        AssertRejectedUnchanged(original, updated);

        var current = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemPath)).InventoryItems,
            item => item.Uid == 10);
        Assert.Equal(currentDiskQuantity, current.StackSize);
        Assert.Equal(1_000, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath)).Money);
    }

    private WorldSession WriteRichStack(string encodingName, string newline)
    {
        var clearClaim = string.Join('\n', LegacyTradeEscrowClaims.ClearValues.Select(pair => pair.Key + "=" + pair.Value));
        var original = WriteSession(encoding: EncodingFor(encodingName), newline: newline, warehouseMode: "both", extra: clearClaim);
        var stack = new PlayerItemSnapshotItem
        {
            Uid = 10, ItemId = 1_000_010, StackSize = 3, Durability = 73, MaximumDurability = 100,
            SocketProgress = 123, SocketOne = 13, SocketTwo = 23, Effect = 200, Plus = 8, Bless = 5,
            Bound = 1, Enchant = 12, Suspicious = 1, PlusProgress = 456, Activate = 1, TimeLeftInMinutes = 789,
            Color = 7, UnlockTimer = 321, ItemPoints = 654, PurificationItemId = 721_258,
            PurificationLevel = 4, PurificationDuration = 1_234, PurificationAddedOn = 638_000_000_000_000_000,
            RefineryEffectId = 11, RefineryEffectLevel = 2, RefineryEffectPercent = 15, RefineryEffectPercent2 = 20,
            RefineryEffectDuration = 4_321, RefineryEffectAddedOn = 638_100_000_000_000_000,
            RuneExperience = 987, Attribute1 = 991, Attribute2 = 992, Attribute3 = 993, Attribute4 = 994,
            Attribute5 = 995, Expiration = 638_200_000_000_000_000, MythSoul = 996, MythSoulPoints = 997,
            PerfectionLevel = 23, PerfectionProgress = 998, OwnerUid = 999,
            OwnerName = "StackOwner", Signature = "StackSignature",
        };
        var items = original.PlayerItems with
        {
            InventoryItems = original.PlayerItems.InventoryItems.Select(item => item.Uid == 10 ? stack : item).ToArray(),
        };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return original with { PlayerItems = items };
    }

    private static WorldSession OneUnitStackResult(WorldSession original) => Updated(original) with
    {
        PlayerItems = original.PlayerItems with
        {
            InventoryItems = original.PlayerItems.InventoryItems.Select(item =>
                item.Uid == 10 ? item.WithStackSize(2) : item).ToArray(),
        },
    };

    private static byte[] WithFirstStackQuantity(byte[] original, ushort quantity)
    {
        // PlayerItemSnapshotLoader's native DBItem layout: 4-byte file count, then
        // UID at record offset0 and the UInt16 StackSize at record offset52.
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(sizeof(int))));
        var result = original.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(sizeof(int) + 52), quantity);
        return result;
    }
}
