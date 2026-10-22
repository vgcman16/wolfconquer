using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Pure managed-list representation of native FloorMap/Inventory.Add behavior;
// concurrent-dictionary enumeration, CheakUp's global counter and SQL are not claimed.
public sealed class LegacyFloorPickupInventoryTests
{
    private const uint ItemId = 50_000;
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Local);

    [Theory]
    [InlineData("65535", (ushort)65_535)]
    [InlineData("65536", (ushort)0)]
    [InlineData("-1", (ushort)0)]
    [InlineData(" +12 ", (ushort)12)]
    public void CatalogStackFieldUsesNativeUshortParsingInsteadOfWidening(string field, ushort expected)
    {
        Assert.Equal(46, ItemType.StackSizeFieldIndex);
        Assert.Equal(expected, Catalog(field).LegacyStackSize);
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-1")]
    [InlineData(null)]
    public void MalformedOrMissingTimeFailsClosedInsteadOfGuessingAnUntimedCatalog(string? time)
    {
        var catalog = Catalog("1", time);
        Assert.False(catalog.TryGetLegacyTime(out _));
        Assert.Equal(0U, catalog.LegacyTime);
        Assert.False(LegacyFloorPickupInventory.TryPlan(Snapshot(), Rich(80, 1), catalog, Now, out var change, out var reason));
        Assert.Null(change);
        Assert.Equal("floorPickupInventoryCatalogTimeInvalid", reason);
    }

    [Theory]
    [InlineData(350_001U)]
    [InlineData(370_001U)]
    public void NativeAccessoryPositionsOverrideCatalogStackLimitToOne(uint itemId)
    {
        var catalog = Catalog("65535", "10", itemId);
        Assert.True(catalog.IsLegacyAccessory);
        Assert.Equal((ushort)1, catalog.LegacyStackSize);
        var change = Plan(Snapshot(), Rich(80, 1).WithItemId(itemId), catalog);
        Assert.Equal(1U, change.Item.Activate);
        Assert.Equal(Now.AddMinutes(10).ToBinary(), change.Item.Expiration);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("merge")]
    [InlineData("whole-stack")]
    public void SendPrefixProjectsExactResultAfterStackSelectionOnEveryNativeAddPath(string path)
    {
        const uint plusStoneId = 730008;
        var destination = Rich(9, 2).WithItemId(plusStoneId).WithMaximumDurabilityAndPlus(0, 12);
        var incoming = Rich(80, path == "whole-stack" ? (ushort)3 : (ushort)1)
            .WithItemId(plusStoneId).WithMaximumDurabilityAndPlus(0, 11);
        var original = path == "append" ? Snapshot() : Snapshot(destination);
        var before = Bytes(original); var incomingBefore = Bytes(Snapshot(incoming));
        var change = Plan(original, incoming, Catalog("10", itemId: plusStoneId, durability: "1234"));
        var merge = path == "merge";
        var expectedItem = merge ? destination.WithStackSize(3) : incoming.WithPosition(0);
        var expected = Bytes(Snapshot(expectedItem));
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 10, 2), 1234);
        expected[4 + 28] = 8; // Native inclusive plus-stone override after clear.
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 48, 4), 0);
        Assert.Equal(expected, Bytes(Snapshot(change.Item)));
        Assert.Equal(merge, change.IsMerge); Assert.Equal(merge ? 9U : 80U, change.Item.Uid);
        var expectedItems = merge || path == "append" ? Snapshot(expectedItem) : Snapshot(destination, expectedItem);
        var expectedWhole = Bytes(expectedItems);
        var outputOffset = 4 + (path == "whole-stack" ? PlayerItemSnapshotLoader.LegacyDbItemSize : 0);
        BinaryPrimitives.WriteUInt16LittleEndian(expectedWhole.AsSpan(outputOffset + 10, 2), 1234);
        expectedWhole[outputOffset + 28] = 8;
        BinaryPrimitives.WriteUInt32LittleEndian(expectedWhole.AsSpan(outputOffset + 48, 4), 0);
        Assert.Equal(expectedWhole, Bytes(change.UpdatedItems));
        Assert.Equal(before, Bytes(original)); Assert.Equal(incomingBefore, Bytes(Snapshot(incoming)));
    }

    [Theory]
    [InlineData(350001U, (ushort)1)]
    [InlineData(370001U, (ushort)1)]
    [InlineData(350001U, (ushort)3)]
    [InlineData(370001U, (ushort)3)]
    public void AccessorySendRunsAfterTimerAndStillActivatesWholeStacksWithoutInventingEndDate(uint itemId, ushort stack)
    {
        var incoming = Rich(80, stack).WithItemId(itemId).WithPosition(9);
        var before = Bytes(Snapshot(incoming));
        var expected = Bytes(Snapshot(incoming.WithPosition(0)));
        var expiration = stack == 1 ? Now.AddMinutes(10).ToBinary() : incoming.Expiration;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 44, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + 48, 4), 0);
        BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(4 + 168, 8), expiration);
        var change = Plan(Snapshot(), incoming, Catalog("65535", "10", itemId));
        Assert.Equal(expected, Bytes(change.UpdatedItems)); Assert.Equal(before, Bytes(Snapshot(incoming)));
        Assert.Equal(expiration, change.Item.Expiration); Assert.Equal(0U, change.Item.TimeLeftInMinutes);
        Assert.Equal((byte)7, change.Item.Plus); Assert.Equal(stack, change.Item.StackSize);
    }

    [Fact]
    public void SinglePickupMergesFirstEnumerationMatchIgnoringBoundPlusAndOtherMetadata()
    {
        var unrelated = Rich(1, 1).WithItemId(ItemId + 1);
        var first = Rich(9, 2); var lowerUidLater = Rich(2, 1);
        var original = Snapshot(unrelated, first, lowerUidLater) with
        {
            WarehouseItems = [Rich(100, 1).WithWarehouseId(1000)],
            EquipmentItems = [Rich(101, 1).WithPosition(4)], RuneItems = [Rich(102, 1).WithPosition(101)],
            RelicFuseItems = [Rich(103, 1).WithPosition(213)], MythsoulBagItems = [Rich(104, 1).WithPosition(217)],
            WardrobeItems = [Rich(105, 1).WithWarehouseId(100)],
        };
        var before = Bytes(original);
        var incoming = Rich(80, 1, plus: 2, bound: 0);
        var catalog = Catalog("10");
        var change = Plan(original, incoming, catalog);
        Assert.True(change.IsMerge);
        Assert.Equal(9U, change.Item.Uid); // Input enumeration order, not UID sort.
        Assert.Same(incoming, change.IncomingItem); Assert.Same(catalog, change.Catalog); Assert.Equal(Now, change.Now);
        Assert.Equal(BytesAfterOrdinarySend(Snapshot(first.WithStackSize(3)), first.Uid), Bytes(Snapshot(change.Item)));
        var expected = original with { InventoryItems = [unrelated, first.WithStackSize(3), lowerUidLater] };
        Assert.Equal(BytesAfterOrdinarySend(expected, first.Uid), Bytes(change.UpdatedItems));
        Assert.Equal(before, Bytes(original));
        Assert.DoesNotContain(change.UpdatedItems.InventoryItems, item => item.Uid == incoming.Uid);
    }

    [Fact]
    public void FloorMapFullInventoryCheckWinsEvenWhenOneStackCouldMerge()
    {
        var original = Snapshot(Enumerable.Range(1, 40).Select(uid => Rich((uint)uid, 1)).ToArray());
        var before = Bytes(original);
        Assert.False(LegacyFloorPickupInventory.TryPlan(original, Rich(80, 1), Catalog("10"), Now, out var change, out var reason));
        Assert.Null(change); Assert.Equal("floorPickupInventoryFull", reason);
        Assert.Equal(before, Bytes(original));
    }

    [Theory]
    [InlineData((ushort)3, "60")]
    [InlineData(ushort.MaxValue, "4294967295")]
    public void IncomingWholeStackBypassesMergeCatalogCapAndTimedActivation(ushort stack, string time)
    {
        var destination = Rich(9, 0);
        var incoming = Rich(80, stack).WithPosition(9);
        var catalog = Catalog("1", time);
        Assert.True(catalog.TryGetLegacyTime(out var parsed)); Assert.Equal(uint.Parse(time), parsed);
        var change = Plan(Snapshot(destination), incoming, catalog);
        Assert.False(change.IsMerge);
        Assert.Equal(BytesAfterOrdinarySend(Snapshot(incoming.WithPosition(0)), incoming.Uid), Bytes(Snapshot(change.Item)));
        Assert.Equal((ushort)0, change.Item.Position); Assert.Equal(stack, change.Item.StackSize);
        Assert.Equal(7U, change.Item.Activate); Assert.Equal(42L, change.Item.Expiration);
        Assert.Equal(0U, change.Item.TimeLeftInMinutes); Assert.Equal(88U, incoming.TimeLeftInMinutes);
        Assert.Same(destination, change.UpdatedItems.InventoryItems[0]);
        Assert.Equal(2, change.UpdatedItems.InventoryItems.Count);
    }

    [Fact]
    public void ZeroStackAppendNormalizesToOneAndPositionZeroButLimitZeroDoesNotActivate()
    {
        var incoming = Rich(80, 0, timeLeft: 77).WithPosition(9);
        var change = Plan(Snapshot(Rich(9, 0)), incoming, Catalog("0", "60"));
        Assert.False(change.IsMerge);
        Assert.Equal(BytesAfterOrdinarySend(Snapshot(incoming.WithStackSize(1).WithPosition(0)), incoming.Uid), Bytes(Snapshot(change.Item)));
        Assert.Equal((ushort)0, incoming.StackSize); Assert.Equal((ushort)9, incoming.Position);
    }

    [Fact]
    public void StackAtCatalogCapIsNotIncrementedOrOverflowedAndIncomingUidIsAppended()
    {
        foreach (var cap in new ushort[] { 3, ushort.MaxValue })
        {
            var capped = Rich(9, cap);
            var change = Plan(Snapshot(capped), Rich(80, 1), Catalog(cap.ToString()));
            Assert.False(change.IsMerge); Assert.Equal(80U, change.Item.Uid);
            Assert.Same(capped, change.UpdatedItems.InventoryItems[0]);
            Assert.Equal(cap, capped.StackSize); Assert.Equal((ushort)1, change.Item.StackSize);
        }
    }

    [Fact]
    public void AppendRetainsNativeWarehouseMetadataWithoutReclassifyingManagedCollectionMembership()
    {
        var incoming = Rich(80, 1).WithWarehouseId(17, position: 9);
        var change = Plan(Snapshot(), incoming, Catalog("0"));
        Assert.False(change.IsMerge);
        Assert.Equal(17U, change.Item.WarehouseId);
        Assert.Equal((ushort)0, change.Item.Position);
        Assert.Same(change.Item, Assert.Single(change.UpdatedItems.InventoryItems));
        Assert.Empty(change.UpdatedItems.WarehouseItems);
        Assert.Equal(BytesAfterOrdinarySend(Snapshot(incoming.WithPosition(0)), incoming.Uid), Bytes(change.UpdatedItems));
        Assert.Equal(17U, incoming.WarehouseId); Assert.Equal((ushort)9, incoming.Position);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void TimedSingleAddRefreshesAppendAndZeroDestinationMergeUsingExactBinaryDateKind(DateTimeKind kind)
    {
        var now = DateTime.SpecifyKind(new DateTime(2026, 10, 21, 12, 0, 0), kind);
        var incoming = Rich(80, 0, timeLeft: 77).WithActivation(0, 42).WithPosition(9);
        var catalog = Catalog("1", "60");
        foreach (var merge in new[] { false, true })
        {
            var destination = Rich(9, 0, timeLeft: 77).WithActivation(0, 42);
            var original = merge ? Snapshot(destination) : Snapshot();
            var before = Bytes(original);
            Assert.True(LegacyFloorPickupInventory.TryPlan(original, incoming, catalog, now, out var change, out var reason), reason);
            var expected = (merge ? destination : incoming.WithPosition(0)).WithStackSize(1)
                .WithActivation(1, now.AddMinutes(60).ToBinary());
            Assert.Equal(merge, change.IsMerge);
            Assert.Equal(BytesAfterOrdinarySend(Snapshot(expected), expected.Uid), Bytes(Snapshot(change.Item)));
            Assert.Equal(now.AddMinutes(60).ToBinary(), change.Item.Expiration);
            Assert.Equal(0U, change.Item.TimeLeftInMinutes);
            Assert.Equal(77U, incoming.TimeLeftInMinutes);
            Assert.Equal(77U, destination.TimeLeftInMinutes);
            Assert.Equal(kind, DateTime.FromBinary(change.Item.Expiration).Kind);
            Assert.Equal(before, Bytes(original));
        }
    }

    [Fact]
    public void UnrepresentableTimedExpirationRejectsWithoutChangingAppendOrMergeInputs()
    {
        foreach (var merge in new[] { false, true })
        {
            var original = merge ? Snapshot(Rich(9, 0)) : Snapshot();
            var before = Bytes(original);
            Assert.False(LegacyFloorPickupInventory.TryPlan(original, Rich(80, 1), Catalog("1", "1"),
                DateTime.MaxValue, out var change, out var reason));
            Assert.Null(change); Assert.Equal("floorPickupInventoryTimeOutsideRange", reason);
            Assert.Equal(before, Bytes(original));
        }
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("owned")]
    [InlineData("duplicate-global")]
    [InlineData("catalog")]
    public void UnsafeUidOrCatalogBindingsCannotRegenerateUidOrSilentlyChooseAnotherImage(string scenario)
    {
        var original = Snapshot(Rich(9, 1)); var incoming = Rich(80, 1); var catalog = Catalog("10");
        if (scenario == "zero") incoming = Rich(0, 1);
        else if (scenario == "owned") incoming = Rich(9, 1);
        else if (scenario == "catalog") catalog = Catalog("10", itemId: ItemId + 1);
        else original = original with { WarehouseItems = [Rich(10, 1).WithWarehouseId(1000).WithDepositedItems([Rich(9, 1)])] };
        var before = Bytes(original);
        Assert.False(LegacyFloorPickupInventory.TryPlan(original, incoming, catalog, Now, out var change, out _));
        Assert.Null(change); Assert.Equal(before, Bytes(original));
    }

    [Fact]
    public void WithActivationChangesOnlyNewActivationProjectionFieldsAndPreservesDepositsAndPerfectionBytes()
    {
        Assert.Equal(38, ItemType.TimeFieldIndex);
        var source = Rich(80, 1).WithDepositedItems([Rich(81, 2)]);
        var original = Bytes(Snapshot(source)); var expected = original.ToArray();
        var binary = Now.AddMinutes(60).ToBinary();
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(sizeof(int) + 44, sizeof(uint)), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(sizeof(int) + 48, sizeof(uint)), 0);
        BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(sizeof(int) + 168, sizeof(long)), binary);
        var updated = source.WithActivation(1, binary);
        Assert.Equal(expected, Bytes(Snapshot(updated)));
        Assert.Equal(original, Bytes(Snapshot(source)));
        Assert.Same(source.DepositedItems, updated.DepositedItems);
        Assert.Equal(binary, Assert.Single(PlayerItemSnapshotLoader.Parse(expected).InventoryItems).Expiration);
    }

    private static LegacyFloorPickupInventoryChange Plan(PlayerItemSnapshot original, PlayerItemSnapshotItem incoming, ItemType catalog)
    {
        Assert.True(LegacyFloorPickupInventory.TryPlan(original, incoming, catalog, Now, out var change, out var reason), reason);
        return change;
    }
    private static PlayerItemSnapshot Snapshot(params PlayerItemSnapshotItem[] items) => new(items, [], [], [], []);
    private static byte[] Bytes(PlayerItemSnapshot items) => PlayerItemSnapshotLoader.ToLegacyBytes(items);
    private static byte[] BytesAfterOrdinarySend(PlayerItemSnapshot items, uint sentUid)
    {
        // Independent DB184 oracle: the ordinary item ID50000 is not equipment,
        // so Send clears its Plus byte28; the native public getter also hides
        // minutes48 for a nonzero Activate. Other records/perfection remain exact.
        var bytes = Bytes(items);
        var offset = sizeof(int);
        foreach (var item in items.InventoryItems)
        {
            if (item.Uid == sentUid)
            {
                Assert.Equal(ItemId, item.ItemId);
                bytes[offset + 28] = 0;
                if (item.Activate != 0)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 48, 4), 0);
                return bytes;
            }
            offset += PlayerItemSnapshotLoader.LegacyDbItemSize * (1 + item.DepositedItems.Count);
        }
        throw new InvalidOperationException("Expected sent item was not in inventory.");
    }
    private static ItemType Catalog(string stack, string? time = "0", uint itemId = ItemId, string durability = "0")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = itemId.ToString(); fields[1] = "PickupCatalog";
        fields[ItemType.StackSizeFieldIndex] = stack;
        fields[ItemType.DurabilityFieldIndex] = durability;
        if (time is null) fields = fields.Take(ItemType.TimeFieldIndex).ToArray(); else fields[ItemType.TimeFieldIndex] = time;
        return new((int)itemId, "PickupCatalog", fields);
    }
    private static PlayerItemSnapshotItem Rich(uint uid, ushort stack, byte plus = 7, byte bound = 1, uint timeLeft = 88) => new()
    {
        Uid = uid, ItemId = ItemId, Durability = 7, MaximumDurability = 11, StackSize = stack,
        SocketProgress = 99, SocketOne = 13, SocketTwo = 3, Effect = 200, Plus = plus, Bless = 1, Bound = bound,
        Enchant = 17, Suspicious = 1, Locked = 1, PlusProgress = 43, Inscribed = 4, Activate = 7,
        TimeLeftInMinutes = timeLeft, Color = 5, UnlockTimer = -42, ItemPoints = 33,
        PurificationItemId = 41, PurificationLevel = 2, PurificationDuration = 55, PurificationAddedOn = 123,
        RefineryEffectId = 99, RefineryEffectLevel = 4, RefineryEffectPercent = 22, RefineryEffectPercent2 = 33,
        RefineryEffectDuration = 44, RefineryEffectAddedOn = 55, AnimaItemId = 4_200_001, RuneExperience = 987,
        Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
        Expiration = 42, MythSoul = 4, MythSoulPoints = 99, PerfectionLevel = 23, PerfectionProgress = 321,
        OwnerUid = 9999, OwnerName = "RichOwner", Signature = "PreserveAllMetadata",
    };
}
