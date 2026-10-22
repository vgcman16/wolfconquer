using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneUnequipPublishesStaleRankAfterExistingManagedRefresh()
    {
        await AssertGuardedRuneUnequipRankingAsync(missingUid: false, activeVendor: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipMissingUidDoesNotPublishRank()
    {
        await AssertGuardedRuneUnequipRankingAsync(missingUid: true, activeVendor: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipActiveVendorRejectsWithDialogAndPreservesState()
    {
        await AssertGuardedRuneUnequipRankingAsync(missingUid: false, activeVendor: true);
    }

    private async Task AssertGuardedRuneUnequipRankingAsync(
        bool missingUid, bool activeVendor, bool stopVendor = false)
    {
        Assert.False(stopVendor && (!activeVendor || missingUid));
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 16, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        // A's live 10300 is newer than the loaded table, which contains only B10000.
        var catalog = CreateRuneRankingCatalog([RuneRankSeed(RuneRankPlayerB, 10_000)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: null, score: 10_300);
        AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);

        if (activeVendor)
        {
            var beforeStartA = sentA.Count;
            var beforeStartB = sentB.Count;
            await handler.HandleFrameAsync(connectionA, ReadFrame(new MsgActionPacket
            {
                ObjId = checked((uint)RuneRankPlayerA),
                Type = MsgActionType.StartVendor,
            }.ToFrameBytes()));
            Assert.Equal(beforeStartA + 2, sentA.Count);
            Assert.Equal(beforeStartB, sentB.Count);
            Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sentA[beforeStartA]), out var booth));
            Assert.Equal(1_393_600U, booth.Uid);
            Assert.Equal(827, booth.X);
            Assert.Equal(565, booth.Y);
            Assert.Equal(406, booth.Mesh);
            Assert.Equal(14, booth.Type);
            Assert.True(MsgActionPacket.TryParse(ReadFrame(sentA[beforeStartA + 1]), out var startEcho));
            Assert.Equal(MsgActionType.StartVendor, startEcho.Type);
            Assert.Equal(booth.Uid, startEcho.DwParam);
            var vending = GetSession(handler, connectionA);
            Assert.Equal("1393600", vending.Character.Attributes["RuntimeVendorUid"]);
            Assert.Same(LegacyVendorListingBook.Empty, vending.LegacyVendorListings);
        }

        if (stopVendor)
        {
            var vendingSession = GetSession(handler, connectionA);
            var beforeStopA = sentA.Count;
            var beforeStopB = sentB.Count;
            await handler.HandleFrameAsync(connectionA, ReadFrame(new MsgActionPacket
            {
                ObjId = checked((uint)RuneRankPlayerA),
                Type = MsgActionType.StopVending,
            }.ToFrameBytes()));
            Assert.Equal(beforeStopA + 1, sentA.Count);
            Assert.Equal(beforeStopB, sentB.Count);
            Assert.Equal(LegacyRemoveEntityPacket.FromEntity(1_393_600U).ToFrameBytes(), sentA[beforeStopA]);
            Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(sentA[beforeStopA]), out var removedBooth));
            Assert.Equal(1_393_600U, removedBooth.Uid);
            var stopped = GetSession(handler, connectionA);
            Assert.Null(stopped.LegacyVendorListings);
            foreach (var key in new[] { "RuntimeVendorUid", "RuntimeVendorMapId", "RuntimeVendorX", "RuntimeVendorY" })
                Assert.False(stopped.Character.Attributes.ContainsKey(key));
            Assert.Equal("1393600", vendingSession.Character.Attributes["RuntimeVendorUid"]);
            Assert.Same(LegacyVendorListingBook.Empty, vendingSession.LegacyVendorListings);
        }
        var vendorBlocked = activeVendor && !stopVendor;

        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var selected = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal((ushort)102, selected.Position);
        Assert.Equal(RuneRankTargetId + 2, selected.ItemId);
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        // Baseline follows real world entry and, when requested, real StartVendor.
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var expectedA = missingUid || vendorBlocked ? originalA : originalA with
        {
            RuneItems = originalA.RuneItems.Select(item =>
                item.Uid == RuneRankTargetUid ? item.WithPosition(212) : item).ToArray(),
        };
        var expectedABytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        const uint absentUid = 5_348_999;
        Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == absentUid);
        var request = CreateRuneEquipmentUse(
            missingUid ? absentUid : RuneRankTargetUid, dwParam: 102, MsgItemUsageAction.UnequipRune);
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        // Vending rejects before lookup; successful non-vending retains the existing managed FIVE frames.
        // The English managed BoxDialog envelope is compatibility, not native NpcReply wire proof.
        Assert.Equal(beforeA + (vendorBlocked ? 1 : missingUid ? 0 : 5), sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        var afterSessionA = GetSession(handler, connectionA);
        if (missingUid || vendorBlocked)
        {
            Assert.Same(originalSessionA, afterSessionA);
            Assert.Same(originalA, afterSessionA.PlayerItems);
            if (vendorBlocked)
                AssertRuneUnequipVendorBoxDialog(sentA[beforeA]);
        }
        else
        {
            Assert.Equal(request, sentA[beforeA]);
            AssertItemUsage(sentA[beforeA], new MsgItemUsagePacket(
                RuneRankTargetUid, 102, MsgItemUsageAction.UnequipRune, 511_043_495, 0, 0, 0));
            AssertRuneUnequipManagedRefresh(sentA, beforeA + 1, afterSessionA);
            Assert.Equal((ushort)212,
                Assert.Single(afterSessionA.PlayerItems.RuneItems, item => item.Uid == RuneRankTargetUid).Position);
            Assert.Equal(originalSessionA with { PlayerItems = afterSessionA.PlayerItems }, afterSessionA);
            Assert.False(originalABytes.SequenceEqual(expectedABytes));
        }

        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(afterSessionA.PlayerItems));
        // Both 102 and 212 count toward native Rune.Score: the successful transfer keeps 10300.
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(afterSessionA.PlayerItems));
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        var reloadedB = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedB));
        if (vendorBlocked)
        {
            Assert.Equal("1393600", afterSessionA.Character.Attributes["RuntimeVendorUid"]);
            Assert.Same(originalSessionA.LegacyVendorListings, afterSessionA.LegacyVendorListings);
            Assert.Same(LegacyVendorListingBook.Empty, afterSessionA.LegacyVendorListings);
            Assert.Empty(afterSessionA.LegacyVendorListings!.Listings);
        }

        // Query B FIRST; A's query would otherwise repair its stale table row by itself.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB,
            rank: missingUid || vendorBlocked ? 1 : 2, score: 10_000);
        if (!missingUid && !vendorBlocked)
        {
            await AssertRuneRankingQueryAsync(
                handler, connectionA, sentA, RuneRankPlayerA, rank: 1, score: 10_300);
        }
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var output = log.ToString();
        Assert.Contains(missingUid || vendorBlocked
            ? "skipped MsgItemUsage unequip rune"
            : "handled MsgItemUsage unequip rune", output);
        if (vendorBlocked)
        {
            Assert.Contains("source rejects active vending.", output);
            Assert.DoesNotContain("rune item was not found.", output);
            Assert.DoesNotContain("handled MsgItemUsage unequip rune", output);
            Assert.DoesNotContain("persisted MsgItemUsage unequip rune", output);
        }
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage unequip rune", output);
    }

    private static void AssertRuneUnequipManagedRefresh(
        IReadOnlyList<byte[]> sent, int start, WorldSession current)
    {
        var uid = checked((uint)current.Character.Uid);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[start]), out var maximum));
        Assert.Equal(uid, maximum.Uid);
        var maxEntry = Assert.Single(maximum.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxEntry.Type);
        Assert.True(maxEntry.Value > 0);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[start + 1]), out var pair));
        Assert.Equal(uid, pair.Uid);
        Assert.Collection(pair.Updates,
            entry =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, entry.Type);
                Assert.Equal(maxEntry.Value, entry.Value);
            },
            entry =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, entry.Type);
                Assert.Equal(checked((ulong)current.Character.MinHitPoints), entry.Value);
                Assert.InRange(entry.Value, 0UL, maxEntry.Value);
            });
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(sent[start + 2]), out var status));
        Assert.Equal(uid, status.Uid);
        Assert.Equal(checked((uint)maxEntry.Value), status.MaxHP);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[start + 3]), out var refine));
        Assert.Equal(new LegacyUserTotalRefineLevPacket(Type: 0, uid, Level: 0), refine);
    }
}
