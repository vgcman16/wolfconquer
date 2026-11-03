using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class SqlOrdinaryAdmissionDataTests
{
    internal static readonly DateTime PreparedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Unspecified);
    internal static byte[] QuestBytes() => Convert.FromHexString("02000000" +
        "010000000000000007000000020000000B00000016000000" + "0200000001000000090000000100000021000000");

    private static PlayerItemSnapshotItem Item(int index, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = checked((uint)(81001 + index)), ItemId = 1100009, Position = position, WarehouseId = warehouse,
        Durability = 25, MaximumDurability = 30, StackSize = 1, Color = 5, Plus = 3,
        PerfectionLevel = checked((uint)(5 + index)), PerfectionProgress = checked((uint)(100 + index)),
        OwnerUid = 9001, OwnerName = "root-" + index, Signature = "signed-" + index,
        DepositedItems = [new() { Uid = checked((uint)(82001 + index)), ItemId = 1088001,
            Durability = 7, MaximumDurability = 9, StackSize = 2, Bound = 1, Color = 4,
            PerfectionLevel = checked((uint)(20 + index)), PerfectionProgress = checked((uint)(200 + index)),
            OwnerUid = 9001, OwnerName = "child-" + index, Signature = "child-signed-" + index }]
    };

    internal static PlayerItemSnapshot AllItemLocations() => new([Item(0)], [Item(1, 213)], [Item(2, 217)],
        [Item(3, 4)], [Item(4, 211)], [Item(5, warehouse: 100)]) { WarehouseItems = [Item(6, warehouse: 8)] };

    internal static byte[] GearBytes() => Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(new(
        11, 22, 33, 12, 1, new(9, 8, 7, 6, 5, 4, 3),
        [new(701, 4, 123, 1, 2, 928000,
            [new uint[] { 101, 102, 103, 104, 105, 106, 107 }, new uint[] { 201, 202, 203, 204, 205, 206, 207 }],
            new uint[] { 11, 12, 13, 14, 15, 16 })], new(2, new uint[] { 301, 302 }))));

    internal static string CharacterText() => Encoding.UTF8.GetString(SqlWorldStateTestData.Character(9001))
        .Replace("Online=1\r\n", "Online=7\r\nHairfaceStorage=1/4201/0/0/\r\n", StringComparison.Ordinal) +
        "[PaidNobility]\r\nIsActive=False\r\nOpaquePaid=kept\r\n";

    internal static SqlWorldStateBundle CompleteBundle(byte[]? character = null)
    {
        Assert.True(SqlWorldStateImage.TryCreate(501, 9001, character ?? Encoding.UTF8.GetBytes(CharacterText()),
            PlayerItemSnapshotLoader.ToLegacyBytes(AllItemLocations()), out var image, out var failure), failure.ToString());
        return SqlWorldStateBundleTests.Bundle(Assert.IsType<SqlWorldStateImage>(image),
        [
            new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.Spells()),
            new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, SqlWorldStateBundleTests.Proficiencies()),
            new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, QuestBytes()),
            new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, GearBytes()),
        ]);
    }

    internal static SqlOrdinaryAdmissionImagePlan Plan(SqlWorldStateBundle bundle)
    {
        Assert.True(SqlOrdinaryAdmissionImagePlan.TryPrepare(bundle, PreparedAt, out var plan, out var reason), reason);
        return Assert.IsType<SqlOrdinaryAdmissionImagePlan>(plan);
    }

    internal static IReadOnlyList<PlayerItemSnapshotItem>[] Locations(PlayerItemSnapshot items) =>
        [items.InventoryItems, items.RelicFuseItems, items.MythsoulBagItems, items.EquipmentItems, items.RuneItems, items.WardrobeItems, items.WarehouseItems];

    internal static void AssertRawBundle(SqlWorldStateBundle expected, SqlWorldStateBundle actual)
    {
        Assert.Equal(expected.AccountId, actual.AccountId); Assert.Equal(expected.CharacterUid, actual.CharacterUid);
        Assert.Equal(expected.Core.CopyCharacterBytes(), actual.Core.CopyCharacterBytes());
        Assert.Equal(expected.Core.CopyItemBytes(), actual.Core.CopyItemBytes()); Assert.Equal(expected.ItemUids.ToArray(), actual.ItemUids.ToArray());
        for (int index = 0; index < 4; index++)
        { Assert.Equal(expected.Sidecars[index].Presence, actual.Sidecars[index].Presence); Assert.Equal(expected.Sidecars[index].CopyBytes(), actual.Sidecars[index].CopyBytes()); }
    }

    internal static byte[][] RawImages(SqlWorldStateBundle source) => new[] { source.Core.CopyCharacterBytes(), source.Core.CopyItemBytes() }
        .Concat(source.Sidecars.ToArray().Select(row => row.CopyBytes())).ToArray();

    internal static void AssertRawUnchanged(byte[][] expected, SqlWorldStateBundle source)
    {
        var actual = RawImages(source); Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }
}
