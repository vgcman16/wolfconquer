using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private async Task AssertOwnedLegacyRuneBagReturnAsync(WorldDataCatalog source, uint bagUid, uint requestUid,
        bool accepted, uint? mergeUid, uint score, int inventoryCount, ushort position, byte bound)
    {
        await WithOwnedLegacyRuneBagAsync(source, async f =>
        {
            const uint actor = 1_293_600, peer = 1_293_601;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, rank: 1, score: score); AssertRuneRankingStartup(sentB, rank: 2, score: 10000);
            var originalA = f.Current(actor); var originalB = f.Current(peer); var before = originalA.PlayerItems;
            Assert.Equal(inventoryCount, before.InventoryItems.Count); Assert.Equal(score, LegacyRuneStoragePacket.ComputeStorageScore(before));
            var target = Assert.Single(before.RuneItems, row => row.Uid == bagUid);
            Assert.Equal(position, target.Position); Assert.Equal(bound, target.Bound);
            var binding = originalA.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
            var oldImage = binding.Items.Image; var oldSpells = originalA.NativeGameClient.MySpells!.ClientSpells;
            var ranks = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
                .GetField("_runeRankings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler));
            var oldRank = ranks.Snapshot(); var state = File.ReadAllBytes(f.Data.State);
            var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before); var peerBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems);
            PlayerItemSnapshotItem? result = null; var expected = before;
            if (accepted)
            {
                result = mergeUid is { } receiver ? Assert.Single(before.InventoryItems, row => row.Uid == receiver)
                    .WithStackSize(checked((ushort)(Math.Max((uint)target.StackSize, 1U) +
                        Math.Max((uint)Assert.Single(before.InventoryItems, row => row.Uid == receiver).StackSize, 1U))))
                    : target.WithUid(checked(OwnedBagHighWater(state) + 1)).WithPosition(0);
                expected = before with
                {
                    InventoryItems = mergeUid is not null ? before.InventoryItems.Select(row => row.Uid == mergeUid ? result! : row).ToArray()
                        : before.InventoryItems.Append(result!).ToArray(),
                    RuneItems = before.RuneItems.Where(row => row.Uid != bagUid).ToArray(),
                };
            }
            var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected); var files = OwnedBagExpectedFiles(f, actor, expected);
            int firstA = sentA.Count, firstB = sentB.Count, observed = 0;
            var request = CreateRuneBagUse(requestUid, 1); Assert.Equal(OwnedBagUsageGolden(requestUid, 1), request);
            f.BeforeSend = (uid, _) =>
            {
                Assert.Equal(actor, uid); Assert.Same(originalB, f.Current(peer)); Assert.Equal(oldRank, ranks.Snapshot());
                AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files); observed++;
            };
            try { await f.SendAsync(actor, request); } finally { f.BeforeSend = null; }
            Assert.Equal(accepted ? 3 : 0, observed); Assert.Equal(firstA + observed, sentA.Count);
            Assert.Equal(firstB, sentB.Count); Assert.Same(originalB, f.Current(peer));
            AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files);
            if (accepted)
            {
                Assert.Equal(OwnedBagRemoveGolden(bagUid), sentA[firstA]);
                Assert.Equal(OwnedBagItemGolden(result!, mergeUid is not null), sentA[firstA + 1]); Assert.Equal(request, sentA[firstA + 2]);
                Assert.DoesNotContain(f.Current(actor).PlayerItems.RuneItems, row => row.Uid == bagUid);
                AssertRuneUntouchedSessionFields(originalA, f.Current(actor));
                Assert.Equal(target.Locked, result!.Locked); Assert.Equal(target.Bound, result.Bound);
                Assert.Equal(inventoryCount + (mergeUid is null ? 1 : 0), f.Current(actor).PlayerItems.InventoryItems.Count);
                if (mergeUid is not null) Assert.Equal((ushort)5, result.StackSize);
                Assert.Contains("committed direct rune bag remove", f.Log.ToString(), StringComparison.Ordinal);
            }
            else
            {
                Assert.Same(originalA, f.Current(actor)); Assert.Same(oldImage, binding.Items.Image);
                Assert.Same(oldSpells, originalA.NativeGameClient.MySpells.ClientSpells); Assert.Equal(oldRank, ranks.Snapshot());
                Assert.DoesNotContain("committed direct rune bag", f.Log.ToString(), StringComparison.Ordinal);
            }
            AssertOwnedBagUidTransition(f, state, accepted && mergeUid is null);
            var expectedIds = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(originalBytes).Where(uid => !accepted || uid != bagUid)
                .Concat(accepted && mergeUid is null ? new[] { result!.Uid } : []);
            Assert.Equal(expectedIds.Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expectedBytes).Order());
            Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(before)); Assert.Equal(peerBytes, File.ReadAllBytes(f.ItemPath(peer)));
            // The real source rank update follows the return echo. B's first
            // later query exposes whether stale A10300 was actually refreshed.
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB, accepted ? 1 : 2, 10000);
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(actor), sentA, RuneRankPlayerA, accepted ? null : 1, score);
            AssertRuneUntouchedSessionFields(originalB, f.Current(peer)); Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems);
            Assert.Equal(expectedBytes, File.ReadAllBytes(f.ItemPath(actor))); Assert.Equal(peerBytes, File.ReadAllBytes(f.ItemPath(peer)));
            AssertOwnedBagConverged(f, actor, files); AssertOwnedBagConverged(f, peer, files);
        }, 1_293_600, 1_293_601);
    }
}
