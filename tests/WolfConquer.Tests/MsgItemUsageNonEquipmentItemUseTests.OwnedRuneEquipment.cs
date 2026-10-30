using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerItemUsageEquipRuneMovesBagRuneToSlotEchoesAndPersists()
    {
        const uint uid = 1_293_600, runeUid = 5_337_530, runeId = 4_010_501;
        var catalog = CreateWorldDataCatalog(CreateItemType(runeId, "RedRune", durability: 1));
        WriteSnapshot(uid, uid, 1_015, 826, 565);
        WriteItems(uid, PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeId, position: 211));
        await WithOwnedRuneDropAsync(catalog, async f =>
        {
            var before = f.Current(uid); int first = f.Frames(uid).Count;
            var expected = before.PlayerItems with
            { RuneItems = before.PlayerItems.RuneItems.Select(row => row.Uid == runeUid ? row.WithPosition(101) : row).ToArray() };
            var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            var expectedFiles = new Dictionary<string, byte[]>(f.Data.Files(), StringComparer.OrdinalIgnoreCase)
            { [Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Items, uid + ".bin"))] = expectedItems };
            var request = CreateRuneEquipmentUse(runeUid, 101, MsgItemUsageAction.EquipRune, 8_888);
            int observed = 0;
            f.BeforeSend = (recipient, _) =>
            {
                Assert.Equal(uid, recipient); if (observed == 0) f.Data.AssertFiles(expectedFiles);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); observed++;
            };
            try { await f.SendAsync(uid, request); } finally { f.BeforeSend = null; }
            var output = f.Frames(uid).Skip(first).ToArray(); Assert.Equal(5, output.Length); Assert.Equal(5, observed);
            Assert.Equal(request, output[0]);
            AssertItemUsage(output[0], new MsgItemUsagePacket(runeUid, 101, MsgItemUsageAction.EquipRune, 511_043_495, 0, 0, 0));
            Assert.True(PacketReader.TryRead(output[0], out var reader)); Assert.Equal(8_888U, reader.ReadUInt32(MsgItemUsagePacket.PayloadLength));
            Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems)); f.Data.AssertFiles(expectedFiles);
            Assert.Equal(before.PlayerSpells.Spells, f.Current(uid).PlayerSpells.Spells);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current(uid).NativeGameClient!.OrdinarySpellBinding!));
            Assert.Contains("committed MsgItemUsage equip rune", f.Log.ToString());
        }, uid);
    }

    [Fact]
    public async Task HandlerItemUsageEquipRuneUnequipsSameRuneAndOccupiedSlotBeforeEquipping()
    {
        const uint uid = 1_293_600, same = 5_337_540, occupied = 5_337_541, selected = 5_337_542;
        const uint selectedId = 4_010_501, occupiedId = 4_010_502;
        var catalog = CreateWorldDataCatalog(CreateItemType(selectedId, "RedRune", durability: 1),
            CreateItemType(occupiedId, "OtherRedRune", durability: 1));
        WriteSnapshot(uid, uid, 1_015, 826, 565);
        WriteItems(uid, PlayerItemSnapshotLoaderTests.DbItem(same, selectedId, 121),
            PlayerItemSnapshotLoaderTests.DbItem(occupied, occupiedId, 101), PlayerItemSnapshotLoaderTests.DbItem(selected, selectedId, 211));
        await WithOwnedRuneDropAsync(catalog, async f =>
        {
            var before = f.Current(uid); int first = f.Frames(uid).Count;
            // Each move keeps the storage ordering established by the preceding
            // displacement: selected first, then occupied, then same-ID Rune.
            var expected = before.PlayerItems with { RuneItems = new[]
            {
                before.PlayerItems.RuneItems.Single(row => row.Uid == selected).WithPosition(101),
                before.PlayerItems.RuneItems.Single(row => row.Uid == occupied).WithPosition(212),
                before.PlayerItems.RuneItems.Single(row => row.Uid == same).WithPosition(212),
            } };
            var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            var expectedFiles = new Dictionary<string, byte[]>(f.Data.Files(), StringComparer.OrdinalIgnoreCase)
            { [Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Items, uid + ".bin"))] = expectedItems };
            var request = CreateRuneEquipmentUse(selected, 101, MsgItemUsageAction.EquipRune);
            int observed = 0;
            f.BeforeSend = (recipient, _) =>
            {
                Assert.Equal(uid, recipient); if (observed == 0) f.Data.AssertFiles(expectedFiles);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); observed++;
            };
            try { await f.SendAsync(uid, request); } finally { f.BeforeSend = null; }
            var output = f.Frames(uid).Skip(first).ToArray(); Assert.Equal(7, output.Length); Assert.Equal(7, observed);
            AssertItemUsage(output[0], new MsgItemUsagePacket(same, 121, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0));
            AssertItemUsage(output[1], new MsgItemUsagePacket(occupied, 101, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0));
            Assert.Equal(request, output[2]);
            Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems)); f.Data.AssertFiles(expectedFiles);
            Assert.Equal(before.PlayerSpells.Spells, f.Current(uid).PlayerSpells.Spells);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current(uid).NativeGameClient!.OrdinarySpellBinding!));
            Assert.Contains("committed MsgItemUsage equip rune", f.Log.ToString());
        }, uid);
    }

    [Fact]
    public async Task HandlerItemUsageUnequipRuneMovesWornRuneToStorageEchoesAndPersists()
    {
        const uint uid = 1_293_600, runeUid = 5_337_550, runeId = 4_020_501;
        var catalog = CreateWorldDataCatalog(CreateItemType(runeId, "OwnedRune", durability: 1));
        WriteSnapshot(uid, uid, 1_015, 826, 565);
        WriteItems(uid, PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeId, 102));
        await WithOwnedRuneDropAsync(catalog, async f =>
        {
            var before = f.Current(uid); int first = f.Frames(uid).Count;
            var expected = before.PlayerItems with
            { RuneItems = before.PlayerItems.RuneItems.Select(row => row.WithPosition(212)).ToArray() };
            var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            var expectedFiles = new Dictionary<string, byte[]>(f.Data.Files(), StringComparer.OrdinalIgnoreCase)
            { [Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Items, uid + ".bin"))] = expectedItems };
            var request = CreateRuneEquipmentUse(runeUid, 102, MsgItemUsageAction.UnequipRune);
            int observed = 0;
            f.BeforeSend = (recipient, _) =>
            {
                Assert.Equal(uid, recipient); if (observed == 0) f.Data.AssertFiles(expectedFiles);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); observed++;
            };
            try { await f.SendAsync(uid, request); } finally { f.BeforeSend = null; }
            var output = f.Frames(uid).Skip(first).ToArray(); Assert.Equal(5, output.Length); Assert.Equal(5, observed);
            Assert.Equal(request, output[0]); AssertRuneUnequipManagedRefresh(output, 1, f.Current(uid));
            Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems)); f.Data.AssertFiles(expectedFiles);
            Assert.Equal(before.PlayerSpells.Spells, f.Current(uid).PlayerSpells.Spells);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current(uid).NativeGameClient!.OrdinarySpellBinding!));
            Assert.Contains("committed MsgItemUsage unequip rune", f.Log.ToString());
        }, uid);
    }
}
