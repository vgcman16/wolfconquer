using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native MsgGameItem.Send233..264 and public minutes getter182..195. Extras, lock responses and
// physical Inventory.Update owners are deliberately not simulated by this proof.
public sealed class LegacyFloorPickupItemSendTests
{
    [Theory]
    [InlineData("0", (ushort)0)]
    [InlineData("65535", ushort.MaxValue)]
    [InlineData(" +123 ", (ushort)123)]
    public void MissingMaximumUsesNativeUshortCatalogWithoutReplacingCurrentDurability(string field, ushort maximum)
    {
        var source = Rich(50000, maximum: 0);
        var before = Bytes(source); var expected = before.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 10, 2), maximum);
        expected[4 + 28] = 0; // This ordinary item is not equipment.
        var catalog = Catalog(50000, field);
        Assert.Equal(21, ItemType.DurabilityFieldIndex);
        Assert.True(catalog.TryGetLegacyDurability(out var parsed)); Assert.Equal(maximum, parsed);
        var actual = Project(source, catalog);
        Assert.Equal(expected, Bytes(actual)); Assert.Equal(before, Bytes(source));
        Assert.Equal((ushort)19, actual.Durability);
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("4294967295")]
    [InlineData("-1")]
    [InlineData("bad")]
    [InlineData(null)]
    public void InvalidMissingMaximumCatalogFailsClosedAndDoesNotChangeItem(string? field)
    {
        var source = Rich(50000, maximum: 0); var before = Bytes(source);
        var catalog = Catalog(50000, field);
        Assert.False(catalog.TryGetLegacyDurability(out _));
        Assert.False(LegacyFloorPickupItemSend.TryProject(source, catalog, out var actual, out var reason));
        Assert.Null(actual); Assert.Equal("floorPickupItemSendCatalogDurabilityInvalid", reason);
        Assert.Equal(before, Bytes(source));
    }

    [Theory]
    [InlineData("65536")]
    [InlineData(null)]
    public void ExistingMaximumIsNotReplacedOrMadeDependentOnCatalogDurability(string? field)
    {
        var source = Rich(410003, maximum: 271);
        var before = Bytes(source); var actual = Project(source, Catalog(410003, field));
        Assert.Same(source, actual); Assert.Equal(before, Bytes(actual));
    }

    [Theory]
    [InlineData(730000U, (byte)0)]
    [InlineData(730001U, (byte)1)]
    [InlineData(730002U, (byte)2)]
    [InlineData(730003U, (byte)3)]
    [InlineData(730004U, (byte)4)]
    [InlineData(730005U, (byte)5)]
    [InlineData(730006U, (byte)6)]
    [InlineData(730007U, (byte)7)]
    [InlineData(730008U, (byte)8)]
    [InlineData(730009U, (byte)0)]
    public void PlusStoneOverrideRunsAfterOrdinaryItemPlusClearingWithInclusiveNativeBounds(uint itemId, byte plus)
    {
        var source = Rich(itemId, plus: 12); var before = Bytes(source); var expected = before.ToArray();
        expected[4 + 28] = plus;
        var actual = Project(source, Catalog(itemId));
        Assert.Equal(expected, Bytes(actual)); Assert.Equal(before, Bytes(source));
        Assert.Equal(plus, actual.Plus);
    }

    [Theory]
    [InlineData(203003U)]
    [InlineData(200000U)]
    [InlineData(199185U)]
    [InlineData(111003U)]
    [InlineData(120001U)]
    [InlineData(130003U)]
    [InlineData(150000U)]
    [InlineData(160013U)]
    [InlineData(201003U)]
    [InlineData(202003U)]
    [InlineData(300000U)]
    [InlineData(410003U)]
    [InlineData(900000U)]
    [InlineData(1050000U)]
    [InlineData(2100025U)]
    [InlineData(204003U)]
    [InlineData(4010000U)]
    [InlineData(4020000U)]
    [InlineData(4030000U)]
    [InlineData(4100000U)]
    [InlineData(616U)]
    [InlineData(681000U)]
    [InlineData(680000U)]
    public void NativeEquipmentPositionsRetainPlusWithoutPositionOrMetadataNormalization(uint itemId)
    {
        var source = Rich(itemId); var before = Bytes(source);
        var actual = Project(source, Catalog(itemId));
        Assert.Same(source, actual); Assert.Equal(before, Bytes(actual));
        Assert.Equal((byte)12, actual.Plus); Assert.Equal((ushort)213, actual.Position);
    }

    [Theory]
    [InlineData(350001U)]
    [InlineData(370001U)]
    [InlineData(360323U)] // Overlap retains the native first accessory branch.
    public void AccessoryActivatesWithZeroPublicSavedMinutesAndPreservesEndDateAndAllOtherMetadata(uint itemId)
    {
        var source = Rich(itemId, maximum: 0); var before = Bytes(source); var expected = before.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 10, 2), 65535);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 44, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 48, 4), 0);
        var actual = Project(source, Catalog(itemId));
        Assert.Equal(expected, Bytes(actual)); Assert.Equal(before, Bytes(source));
        Assert.Equal(1U, actual.Activate); Assert.Equal(0U, actual.TimeLeftInMinutes);
        Assert.Equal(source.Expiration, actual.Expiration); Assert.Same(source.DepositedItems, actual.DepositedItems);
        Assert.Equal((byte)12, actual.Plus); Assert.Equal(77U, source.TimeLeftInMinutes);
    }

    [Theory]
    [InlineData(0U, 0U)]
    [InlineData(0U, 77U)]
    [InlineData(1U, 0U)]
    [InlineData(1U, 77U)]
    [InlineData(2U, 0U)]
    [InlineData(2U, 77U)]
    [InlineData(7U, 0U)]
    [InlineData(7U, 77U)]
    [InlineData(uint.MaxValue, 0U)]
    [InlineData(uint.MaxValue, 77U)]
    public void NativePublicMinutesGetterHidesRawMinutesForEveryNonzeroActivationWithoutChangingActivationOrExpiration(
        uint activate, uint rawMinutes)
    {
        var source = Rich(410003, activate: activate, rawMinutes: rawMinutes);
        var before = Bytes(source); var expected = before.ToArray();
        var visibleMinutes = activate == 0 ? rawMinutes : 0U;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 48, 4), visibleMinutes);
        var actual = Project(source, Catalog(410003));
        Assert.Equal(expected, Bytes(actual)); Assert.Equal(before, Bytes(source));
        Assert.Equal(activate, actual.Activate); Assert.Equal(source.Expiration, actual.Expiration);
        Assert.Equal(visibleMinutes, actual.TimeLeftInMinutes); Assert.Equal(rawMinutes, source.TimeLeftInMinutes);
        Assert.Same(source.DepositedItems, actual.DepositedItems);
    }

    [Fact]
    public void CatalogIdentityMismatchAndNullInputsCannotProjectAnotherItem()
    {
        var source = Rich(50000);
        foreach (var pair in new (PlayerItemSnapshotItem Item, ItemType Catalog)[]
            { (source, Catalog(50001)), (null!, Catalog(50000)), (source, null!), (source, Catalog(0)) })
        {
            Assert.False(LegacyFloorPickupItemSend.TryProject(pair.Item, pair.Catalog, out var actual, out var reason));
            Assert.Null(actual); Assert.Equal("floorPickupItemSendBindingInvalid", reason);
        }
    }

    private static PlayerItemSnapshotItem Project(PlayerItemSnapshotItem item, ItemType catalog)
    {
        Assert.True(LegacyFloorPickupItemSend.TryProject(item, catalog, out var projected, out var reason), reason);
        return projected;
    }

    private static ItemType Catalog(uint itemId, string? durability = "65535")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(); fields[1] = "NativeSend";
        if (durability is null) fields = fields.Take(21).ToArray(); else fields[21] = durability;
        return new((int)itemId, "NativeSend", fields);
    }

    private static byte[] Bytes(PlayerItemSnapshotItem item) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new([item], [], [], [], []));

    private static PlayerItemSnapshotItem Rich(uint itemId, ushort maximum = 271, byte plus = 12,
        uint activate = 0, uint rawMinutes = 77) => new()
    {
        Uid = 80, ItemId = itemId, Durability = 19, MaximumDurability = maximum, Position = 213,
        SocketProgress = 99, SocketOne = 13, SocketTwo = 3, Effect = 200, Plus = plus, Bless = 1, Bound = 1,
        Enchant = 17, Suspicious = 1, Locked = 1, PlusProgress = 43, Inscribed = 4, Activate = activate,
        TimeLeftInMinutes = rawMinutes, StackSize = 7, WarehouseId = 1000, Color = 5, UnlockTimer = -42, ItemPoints = 33,
        PurificationItemId = 41, PurificationLevel = 2, PurificationDuration = 55, PurificationAddedOn = 123,
        RefineryEffectId = 99, RefineryEffectLevel = 4, RefineryEffectPercent = 22, RefineryEffectPercent2 = 33,
        RefineryEffectDuration = 44, RefineryEffectAddedOn = 55, AnimaItemId = 4200001, RuneExperience = 987,
        Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
        Expiration = new DateTime(2026, 11, 1, 12, 30, 0, DateTimeKind.Local).ToBinary(),
        MythSoul = 4, MythSoulPoints = 99, PerfectionLevel = 23, PerfectionProgress = 321,
        OwnerUid = 9999, OwnerName = "RichOwner", Signature = "PreserveAllMetadata",
        DepositedItems = [new() { Uid = 81, ItemId = 50001, StackSize = 2, Plus = 9, PerfectionLevel = 1, OwnerName = "Child" }],
    };
}
