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
        var seeded = CreateRuneBagRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)], includeTransferMetadata: true);
        // The native constructor still receives its actual class/raw25. Only
        // the required bag raw46 column is absent in this declared negative.
        var catalog = OwnedLegacyRuneBagCatalog(seeded, metadataPresent ? null : RuneBagRankingTransferItemId);
        await WithOwnedLegacyRuneBagAsync(catalog, async f =>
        {
            const uint actor = 1_293_600, peer = 1_293_601;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, rank: 1, score: 9_700);
            AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
            var originalA = f.Current(actor); var originalB = f.Current(peer);
            var beforeA = PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems);
            var beforeB = PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems);
            Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA.PlayerItems));
            Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB.PlayerItems));
            Assert.Equal(eligible ? 140L : 14L, originalA.Character.Level); Assert.Equal(2L, originalA.Character.Reborn);
            var transfer = Assert.Single(intoBag ? originalA.PlayerItems.InventoryItems : originalA.PlayerItems.RuneItems,
                row => row.Uid == RuneBagRankingTransferUid);
            Assert.Equal(RuneBagRankingTransferItemId, transfer.ItemId); Assert.Equal((ushort)1, transfer.StackSize);
            Assert.Equal((byte)0, transfer.Locked); Assert.Equal((ushort)(intoBag ? 0 : 211), transfer.Position);
            var state = File.ReadAllBytes(f.Data.State);
            bool accepted = eligible && !missing && metadataPresent;
            var moved = intoBag ? transfer.WithPosition(211) : transfer.WithUid(checked(OwnedBagHighWater(state) + 1)).WithPosition(0);
            var expected = !accepted ? originalA.PlayerItems : intoBag ? originalA.PlayerItems with
            {
                InventoryItems = originalA.PlayerItems.InventoryItems.Where(row => row.Uid != transfer.Uid).ToArray(),
                RuneItems = originalA.PlayerItems.RuneItems.Append(moved).OrderBy(row => row.Position).ToArray(),
            } : originalA.PlayerItems with
            {
                InventoryItems = originalA.PlayerItems.InventoryItems.Append(moved).ToArray(),
                RuneItems = originalA.PlayerItems.RuneItems.Where(row => row.Uid != transfer.Uid).ToArray(),
            };
            var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(expected));
            var files = OwnedBagExpectedFiles(f, actor, expected); int firstA = sentA.Count, firstB = sentB.Count, observed = 0;
            var request = CreateRuneBagUse(missing ? RuneBagRankingMissingUid : RuneBagRankingTransferUid, intoBag ? 0UL : 1UL);
            Assert.Equal(OwnedBagUsageGolden(missing ? RuneBagRankingMissingUid : RuneBagRankingTransferUid, intoBag ? 0UL : 1UL), request);
            f.BeforeSend = (uid, _) =>
            {
                Assert.Equal(actor, uid); Assert.Same(originalB, f.Current(peer));
                AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files); observed++;
            };
            try { await f.SendAsync(actor, request); } finally { f.BeforeSend = null; }
            Assert.Equal(firstB, sentB.Count); Assert.Same(originalB, f.Current(peer));
            Assert.Equal(accepted ? intoBag ? 12 : 3 : 0, observed);
            Assert.Equal(firstA + observed, sentA.Count);
            AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files);
            if (accepted)
            {
                int index = firstA;
                if (!intoBag) Assert.Equal(OwnedBagRemoveGolden(RuneBagRankingTransferUid), sentA[index++]);
                Assert.Equal(OwnedBagItemGolden(moved), sentA[index++]); Assert.Equal(request, sentA[index++]);
                if (intoBag) LegacySelectedEquipmentQueryAssertions.NeutralQuery(sentA, index, actor);
                AssertRuneUntouchedSessionFields(originalA, f.Current(actor));
                Assert.Contains("committed direct rune bag " + (intoBag ? "add" : "remove"), f.Log.ToString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.Same(originalA, f.Current(actor));
                Assert.DoesNotContain("committed direct rune bag", f.Log.ToString(), StringComparison.Ordinal);
            }
            AssertOwnedBagUidTransition(f, state, accepted && !intoBag);
            Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath(actor)));
            Assert.Equal(beforeB, File.ReadAllBytes(f.ItemPath(peer)));
            Assert.Equal(beforeA, PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems));
            Assert.Equal(beforeB, PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems));

            // B's first later query cannot repair stale A by updating B itself.
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB,
                rank: accepted ? 1 : 2, score: 10_000);
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(actor), sentA, RuneRankPlayerA,
                rank: accepted ? null : 1, score: 9_700);
            Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath(actor))); Assert.Equal(beforeB, File.ReadAllBytes(f.ItemPath(peer)));
            AssertRuneUntouchedSessionFields(originalB, f.Current(peer)); Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems);
            AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files);
        }, 1_293_600, 1_293_601);
    }

    private void WriteRuneBagRankingPlayerA(bool intoBag, bool eligible)
    {
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1_015, x: 826, y: 565, level: eligible ? 140 : 14);
        var transfer = PlayerItemSnapshotLoaderTests.DbItem(RuneBagRankingTransferUid, RuneBagRankingTransferItemId,
            position: intoBag ? (ushort)0 : (ushort)211, durability: 100, maximumDurability: 100, stackSize: 1);
        var target = PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1);
        var stored = PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1);
        if (intoBag) WriteItems(RuneRankPlayerA, transfer, target, stored);
        else WriteItems(RuneRankPlayerA, target, transfer, stored);
    }
}
