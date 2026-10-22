using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneBagRankingTransferUid = 5_348_950;
    private const uint RuneBagRankingMissingUid = 5_348_998;
    private const uint RuneBagRankingTransferItemId = RuneRankTargetId + 3;

    [Fact]
    public async Task HandlerRuneBagAddRefreshesStaleSharedRankWithoutScoreChange()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: true, missing: false, eligible: true);
    }

    [Fact]
    public async Task HandlerRuneBagRemoveRefreshesStaleSharedRankWithoutScoreChange()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: false, missing: false, eligible: true);
    }

    [Fact]
    public async Task HandlerRuneBagMissingInventoryDoesNotRefreshSharedRank()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: true, missing: true, eligible: true);
    }

    [Fact]
    public async Task HandlerRuneBagMissingBagItemDoesNotRefreshSharedRank()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: false, missing: true, eligible: true);
    }

    [Fact]
    public async Task HandlerRuneBagIneligibleAddDoesNotRefreshSharedRank()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: true, missing: false, eligible: false);
    }

    [Fact]
    public async Task HandlerRuneBagIneligibleRemoveDoesNotRefreshSharedRank()
    {
        await AssertRuneBagRankingTransferAsync(intoBag: false, missing: false, eligible: false);
    }

    [Fact]
    public async Task HandlerRuneBagRemoveWithoutCatalogMetadataDoesNotRefreshSharedRank()
    {
        await AssertRuneBagRankingTransferAsync(
            intoBag: false, missing: false, eligible: true, metadataPresent: false);
    }

    private async Task AssertRuneBagRankingTransferAsync(
        bool intoBag, bool missing, bool eligible, bool metadataPresent = true)
    {
        WriteRuneBagRankingPlayerA(intoBag, eligible);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneBagRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)], metadataPresent);
        Assert.Equal(metadataPresent, catalog.ItemsById.ContainsKey((int)RuneBagRankingTransferItemId));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: 1, score: 9_700);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
        Assert.Equal(eligible ? 140L : 14L, originalSessionA.Character.Level);
        Assert.Equal(2L, originalSessionA.Character.Reborn);
        var transfer = Assert.Single(
            intoBag ? originalA.InventoryItems : originalA.RuneItems,
            item => item.Uid == RuneBagRankingTransferUid);
        Assert.Equal(RuneBagRankingTransferItemId, transfer.ItemId);
        Assert.Equal((ushort)(intoBag ? 0 : 211), transfer.Position);
        Assert.Equal((ushort)1, transfer.StackSize);
        Assert.Equal((byte)0, transfer.Plus);
        Assert.Equal((byte)0, transfer.Locked);
        Assert.DoesNotContain(originalA.InventoryItems, item => item.Uid == RuneBagRankingMissingUid);
        Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == RuneBagRankingMissingUid);
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        // Capture the ordinary physical baseline only AFTER both entries complete.
        AssertRuneBagRankingPhysicalFiles(originalABytes, originalBBytes);
        var accepted = eligible && !missing;
        var publishesRank = accepted && (intoBag || metadataPresent);
        var moved = transfer.WithPosition((ushort)(intoBag ? 211 : 0));
        var expectedA = !accepted ? originalA : intoBag
            ? originalA with
            {
                InventoryItems = originalA.InventoryItems.Where(item => item.Uid != transfer.Uid).ToArray(),
                RuneItems = originalA.RuneItems.Append(moved).OrderBy(item => item.Position).ToArray(),
            }
            : originalA with
            {
                InventoryItems = originalA.InventoryItems.Append(moved).ToArray(),
                RuneItems = originalA.RuneItems.Where(item => item.Uid != transfer.Uid).ToArray(),
            };
        var expectedABytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        // Neither inventory position0 nor bag position211 contributes to Rune.Score.
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(expectedA));
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        var request = CreateRuneBagUse(
            missing ? RuneBagRankingMissingUid : RuneBagRankingTransferUid,
            dwParam: intoBag ? 0UL : 1UL);

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        var outgoing = sentA.Skip(beforeA).ToArray();
        Assert.Equal(beforeB, sentB.Count);
        Assert.Equal(accepted ? (intoBag ? 6 : 3) : 0, outgoing.Length);
        var currentSessionA = GetSession(handler, connectionA);
        if (accepted)
        {
            Assert.NotSame(originalSessionA, currentSessionA);
            var itemIndex = intoBag ? 0 : 1;
            if (!intoBag)
            {
                var removal = new MsgItemUsagePacket(
                    RuneBagRankingTransferUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0);
                AssertItemUsage(outgoing[0], removal);
                Assert.Equal(removal.ToFrameBytes(), outgoing[0]);
            }
            var expectedItem = LegacyGameItemPacket.FromSnapshotItem(
                moved, LegacyGameItemPacket.CreateRuneSpellOverride(moved, catalog.ItemsById));
            Assert.Equal(expectedItem.ToFrameBytes(), outgoing[itemIndex]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(outgoing[itemIndex]), out var itemPacket));
            Assert.Equal(RuneBagRankingTransferUid, itemPacket.Uid);
            Assert.Equal((byte)(intoBag ? 211 : 0), itemPacket.Position);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
            Assert.Equal(request, outgoing[itemIndex + 1]);
            if (intoBag)
                AssertRuneBagRankingEquipmentSuffix(outgoing, start: 2, RuneRankPlayerA);
            foreach (var uid in new[] { RuneRankTargetUid, RuneRankStoredUid })
            {
                Assert.Same(
                    Assert.Single(originalA.RuneItems, item => item.Uid == uid),
                    Assert.Single(currentSessionA.PlayerItems.RuneItems, item => item.Uid == uid));
            }
        }
        else
        {
            Assert.Same(originalSessionA, currentSessionA);
        }
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(currentSessionA.PlayerItems));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(currentSessionA.PlayerItems));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(accepted, !originalABytes.AsSpan().SequenceEqual(expectedABytes));
        AssertRuneBagRankingPhysicalFiles(expectedABytes, originalBBytes);

        // B's FIRST later query cannot repair A's stale10300 row by updating B itself.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: publishesRank ? 1 : 2, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: publishesRank ? null : 1, score: 9_700);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(GetSession(handler, connectionA).PlayerItems));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(GetSession(handler, connectionB).PlayerItems));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        AssertRuneBagRankingPhysicalFiles(expectedABytes, originalBBytes);
        var output = log.ToString();
        var direction = intoBag ? "add" : "remove";
        if (accepted)
        {
            Assert.Contains("handled MsgItemUsage direct rune bag " + direction, output);
            Assert.Contains("persisted MsgItemUsage direct rune bag " + direction + " items", output);
        }
        else
        {
            Assert.Contains("skipped MsgItemUsage direct rune bag", output);
            Assert.DoesNotContain("handled MsgItemUsage direct rune bag " + direction, output);
            if (!eligible)
                Assert.Contains("requires second reborn and level 15", output);
        }
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune bag", output);
    }

    private void WriteRuneBagRankingPlayerA(bool intoBag, bool eligible)
    {
        WriteSnapshot(
            accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1_015, x: 826, y: 565,
            level: eligible ? 140 : 14);
        var transfer = PlayerItemSnapshotLoaderTests.DbItem(
            RuneBagRankingTransferUid, RuneBagRankingTransferItemId, position: (ushort)(intoBag ? 0 : 211),
            durability: 100, maximumDurability: 100, stackSize: 1);
        var target = PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1);
        var stored = PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1);
        // Preserve physical canonical inventory-first, then increasing rune positions.
        if (intoBag)
            WriteItems(RuneRankPlayerA, transfer, target, stored);
        else
            WriteItems(RuneRankPlayerA, target, transfer, stored);
    }

    private void AssertRuneBagRankingPhysicalFiles(byte[] expectedA, byte[] expectedB)
    {
        Assert.Equal(expectedA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var a = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        var b = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB);
        Assert.Equal(expectedA, PlayerItemSnapshotLoader.ToLegacyBytes(a));
        Assert.Equal(expectedB, PlayerItemSnapshotLoader.ToLegacyBytes(b));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(a));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(b));
    }

    private static void AssertRuneBagRankingEquipmentSuffix(IReadOnlyList<byte[]> frames, int start, long uid)
    {
        // Preserve the existing managed four-frame refresh; this is NOT native full QueryEquipment proof.
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[start]), out var maximumPacket));
        Assert.Equal(checked((uint)uid), maximumPacket.Uid);
        var maximum = Assert.Single(maximumPacket.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[start + 1]), out var hitpointsPacket));
        Assert.Equal(checked((uint)uid), hitpointsPacket.Uid);
        Assert.Collection(hitpointsPacket.Updates,
            entry =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, entry.Type);
                Assert.Equal(maximum.Value, entry.Value);
            },
            entry =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, entry.Type);
                Assert.InRange(entry.Value, 0UL, maximum.Value);
            });
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(frames[start + 2]), out var status));
        Assert.Equal(checked((uint)uid), status.Uid);
        Assert.Equal((ulong)status.MaxHP, maximum.Value);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(frames[start + 3]), out var refinement));
        Assert.Equal(checked((uint)uid), refinement.Uid);
    }
}
