using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class FloorItemRuntimeStateStoreTests
{
    [Fact]
    public void AddVisiblePacketStoresPacketByFloorUid()
    {
        var store = new FloorItemRuntimeStateStore();
        var packet = VisiblePacket(uid: 900_028);

        store.AddVisiblePacket(packet);

        Assert.True(store.TryGetVisiblePacket(packet.Uid, out var stored));
        Assert.Same(packet, stored);
        Assert.Equal(1_002_000U, stored.ItemId);
        Assert.Equal(841, stored.X);
        Assert.Equal(573, stored.Y);
        Assert.Equal(1_293_600U, stored.ItemOwnerUid);
    }

    [Fact]
    public void TryConsumePickupConsumesFloorUidOnce()
    {
        var store = new FloorItemRuntimeStateStore();
        var packet = VisiblePacket(uid: 900_029);

        store.AddVisiblePacket(packet);

        Assert.True(store.TryConsumePickup(packet.Uid, out var consumed));
        Assert.Same(packet, consumed);
        Assert.False(store.TryConsumePickup(packet.Uid, out _));
        Assert.False(store.TryGetVisiblePacket(packet.Uid, out _));
    }

    [Fact]
    public void AddVisibleStoresRuntimeEntryMetadata()
    {
        var store = new FloorItemRuntimeStateStore();
        var packet = VisiblePacket(uid: 900_033);
        var entry = FloorItemRuntimeEntry.ForMoney(mapId: 1015, packet, amount: 50_000);

        store.AddVisible(entry);

        Assert.True(store.TryGetVisibleEntry(packet.Uid, out var stored));
        Assert.Equal(1015, stored.MapId);
        Assert.Same(packet, stored.Packet);
        Assert.Equal(FloorItemRuntimePickupKind.Money, stored.Kind);
        Assert.Equal(50_000, stored.Amount);
        Assert.True(store.TryConsumePickupEntry(packet.Uid, out var consumed));
        Assert.Equal(stored, consumed);
        Assert.False(store.TryConsumePickupEntry(packet.Uid, out _));
    }

    [Fact]
    public void OwnerOnlyDropBlocksOtherPlayersUntilLegacyOwnerWindowExpires()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var packet = VisiblePacket(uid: 900_034);
        var entry = FloorItemRuntimeEntry.ForMoney(mapId: 1015, packet, amount: 50_000, ownerOnly: true);

        store.AddVisible(entry, now);

        Assert.Equal(
            FloorItemRuntimePickupStatus.OwnerLocked,
            store.GetPickupStatus(packet.Uid, playerUid: 1_293_601, now.AddSeconds(20), out _));
        Assert.Equal(
            FloorItemRuntimePickupStatus.Available,
            store.GetPickupStatus(packet.Uid, playerUid: 1_293_601, now.AddSeconds(21), out var available));
        Assert.Same(packet, available.Packet);
        Assert.Equal(
            FloorItemRuntimePickupStatus.Available,
            store.TryConsumePickupEntry(packet.Uid, playerUid: 1_293_601, now.AddSeconds(21), out var consumed));
        Assert.Equal(entry, consumed);
    }

    [Fact]
    public void OwnerOnlyDropCanBeConsumedWithExplicitLegacyTeamBypass()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var packet = VisiblePacket(uid: 900_037);
        var entry = FloorItemRuntimeEntry.ForMoney(mapId: 1015, packet, amount: 50_000, ownerOnly: true);

        store.AddVisible(entry, now);

        Assert.Equal(
            FloorItemRuntimePickupStatus.OwnerLocked,
            store.GetPickupStatus(packet.Uid, playerUid: 1_293_601, now.AddSeconds(5), out _));
        Assert.Equal(
            FloorItemRuntimePickupStatus.Available,
            store.TryConsumePickupEntry(
                packet.Uid,
                playerUid: 1_293_601,
                allowOwnerLockBypass: true,
                now.AddSeconds(5),
                out var consumed));
        Assert.Equal(entry, consumed);
        Assert.False(store.TryGetVisibleEntry(packet.Uid, out _));
    }

    [Fact]
    public void PublicDropWithOwnerUidCanBePickedUpImmediatelyByOtherPlayers()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var packet = VisiblePacket(uid: 900_035);
        var entry = FloorItemRuntimeEntry.ForItem(mapId: 1015, packet, ownerOnly: false);

        store.AddVisible(entry, now);

        Assert.Equal(
            FloorItemRuntimePickupStatus.Available,
            store.GetPickupStatus(packet.Uid, playerUid: 1_293_601, now, out var available));
        Assert.Same(packet, available.Packet);
    }

    [Fact]
    public void VisibleDropExpiresAfterLegacyFloorItemLifetime()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var packet = VisiblePacket(uid: 900_036);
        var entry = FloorItemRuntimeEntry.ForItem(mapId: 1015, packet);

        store.AddVisible(entry, now);

        Assert.True(store.TryGetVisibleEntry(packet.Uid, now.AddSeconds(39), out _));
        Assert.False(store.TryGetVisibleEntry(packet.Uid, now.AddSeconds(40), out _));
        Assert.Equal(
            FloorItemRuntimePickupStatus.Missing,
            store.GetPickupStatus(packet.Uid, playerUid: 1_293_600, now.AddSeconds(41), out _));
    }

    [Fact]
    public void AddVisibleStoresLegacyFloorEffectPacketsForRuntimeRefresh()
    {
        var store = new FloorItemRuntimeStateStore();
        var packet = VisiblePacket(uid: 900_041) with
        {
            ItemId = 1_080,
            DropType = LegacyFloorItemDropType.Effect,
            PlusOrColor = 14,
            Name = "trap",
        };
        var entry = FloorItemRuntimeEntry.ForItem(mapId: 1015, packet);

        store.AddVisible(entry);

        Assert.True(store.TryGetVisiblePacket(packet.Uid, out var stored));
        Assert.Equal(packet, stored);
        Assert.Equal(LegacyFloorItemDropType.Effect, stored.DropType);
    }

    [Fact]
    public void ConsumeDueEffectsRemovesOnlyDueMatchingSpellEffects()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-12T12:00:00Z");
        var duePacket = VisiblePacket(uid: 900_042) with
        {
            ItemId = 1_080,
            DropType = LegacyFloorItemDropType.Effect,
            OwnerX = 830,
            OwnerY = 584,
            Name = "trap",
        };
        var laterPacket = VisiblePacket(uid: 900_043) with
        {
            ItemId = 1_080,
            DropType = LegacyFloorItemDropType.Effect,
            Name = "trap",
        };
        var due = FloorItemRuntimeEntry.ForEffect(
            mapId: 1015,
            duePacket,
            new FloorItemRuntimeEffect(14_250, duePacket.OwnerX, duePacket.OwnerY, now));
        var later = FloorItemRuntimeEntry.ForEffect(
            mapId: 1015,
            laterPacket,
            new FloorItemRuntimeEffect(14_250, laterPacket.OwnerX, laterPacket.OwnerY, now.AddSeconds(1)));

        store.AddVisible(due, now.AddSeconds(-4));
        store.AddVisible(later, now);

        var consumed = store.ConsumeDueEffects(14_250, now);

        Assert.Equal([due], consumed);
        Assert.False(store.TryGetVisiblePacket(duePacket.Uid, now, out _));
        Assert.True(store.TryGetVisiblePacket(laterPacket.Uid, now, out _));
    }

    [Fact]
    public void SnapshotVisibleReturnsCurrentEntriesAndRemovesExpiredDrops()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var current = FloorItemRuntimeEntry.ForItem(mapId: 1015, VisiblePacket(uid: 900_039));
        var expired = FloorItemRuntimeEntry.ForItem(mapId: 1015, VisiblePacket(uid: 900_040));

        store.AddVisible(expired, now.AddSeconds(-40));
        store.AddVisible(current, now);

        var entries = store.SnapshotVisible(now);

        Assert.Equal([current], entries);
        Assert.False(store.TryGetVisibleEntry(expired.Packet.Uid, now, out _));
        Assert.True(store.TryGetVisibleEntry(current.Packet.Uid, now, out _));
    }

    [Fact]
    public void TryAddVisibleOnGroundScattersAroundOccupiedOrigin()
    {
        var store = new FloorItemRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var first = FloorItemRuntimeEntry.ForItem(mapId: 1015, VisiblePacket(uid: 900_037));
        var second = FloorItemRuntimeEntry.ForItem(mapId: 1015, VisiblePacket(uid: 900_038));

        Assert.True(store.TryAddVisibleOnGround(first, now, range: 0, isGroundValid: null, out var addedFirst));
        Assert.True(store.TryAddVisibleOnGround(second, now, range: 0, isGroundValid: null, out var addedSecond));

        Assert.Equal(841, addedFirst.Packet.X);
        Assert.Equal(573, addedFirst.Packet.Y);
        Assert.Equal(840, addedSecond.Packet.X);
        Assert.Equal(572, addedSecond.Packet.Y);
    }

    [Fact]
    public void MissingUidReturnsFalse()
    {
        var store = new FloorItemRuntimeStateStore();

        Assert.False(store.TryGetVisiblePacket(900_030, out _));
        Assert.False(store.TryConsumePickup(900_030, out _));
    }

    [Fact]
    public void AddVisiblePacketEvictsOldestPacketWhenCapacityIsReached()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var first = VisiblePacket(uid: 900_031);
        var second = VisiblePacket(uid: 900_032);

        store.AddVisiblePacket(first);
        store.AddVisiblePacket(second);

        Assert.False(store.TryGetVisiblePacket(first.Uid, out _));
        Assert.True(store.TryGetVisiblePacket(second.Uid, out var stored));
        Assert.Same(second, stored);
    }

    private static LegacyFloorItemPacket VisiblePacket(uint uid)
    {
        return new LegacyFloorItemPacket(
            uid,
            ItemId: 1_002_000,
            X: 841,
            Y: 573,
            MaxLife: 40,
            LegacyFloorItemDropType.Visible,
            Life: 0,
            PlusOrColor: 3,
            ItemOwnerUid: 1_293_600,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            Name: "");
    }
}
