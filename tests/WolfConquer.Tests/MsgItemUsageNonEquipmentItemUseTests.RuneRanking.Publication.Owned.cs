using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    // The retired unowned Compose fixture cannot exercise publication. These
    // controls use the actual admitted Runtime/Store/native owner and joint WAL.
    // Rank is committed before transport; a later holder cannot rewrite it.
    private async Task AssertOwnedRuneRankPublicationAsync(RuneRankPostSendChange change)
    {
        WriteOwnedRuneComposeRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: true);
        WriteOwnedRuneComposeRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        await WithOwnedRuneComposeAsync(CreateRuneRankingCatalog([RuneRankSeed(RuneRankPlayerB, 10_000)]), async f =>
        {
            const uint actor = 1_293_600, peer = 1_293_601;
            var connection = f.Connection(actor); var sessions = f.Store;
            var owner = GetRuneRankingOwner(f.Handler);
            var original = f.Current(actor); var peerBefore = f.Current(peer);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, actor, out var identity));
            var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems);
            var target = Assert.Single(original.PlayerItems.RuneItems, row => row.Uid == RuneRankTargetUid);
            var expectedTarget = target.WithItemId(RuneRankTargetId + 2).WithRuneExperience(0);
            var expectedItems = original.PlayerItems with
            {
                InventoryItems = [],
                RuneItems = original.PlayerItems.RuneItems.Select(row => row.Uid == RuneRankTargetUid ? expectedTarget : row).ToArray(),
            };
            var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
            var files = OwnedComposeExpectedFiles(f, actor, expectedBytes);
            int firstA = f.Frames(actor).Count, firstB = f.Frames(peer).Count, observed = 0;
            var fired = false;
            WorldSession? committedAtTarget = null;
            f.BeforeSend = (uid, bytes) =>
            {
                Assert.Equal(actor, uid);
                AssertOwnedComposeConverged(f, actor, files);
                AssertOwnedComposeConverged(f, peer, files);
                Assert.Same(peerBefore, f.Current(peer));
                Assert.Equal(new LegacyRuneRankingEntry(actor, original.Character.Name, 10_300),
                    Assert.Single(owner.Snapshot(), row => row.CharacterUid == actor));
                observed++;
                if (!LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) ||
                    item.Uid != RuneRankTargetUid || item.Mode != LegacyGameItemPacket.UpdateItemMode) return;
                Assert.False(fired); fired = true;
                var current = f.Current(actor); committedAtTarget = current;
                if (change is RuneRankPostSendChange.Removed or RuneRankPostSendChange.RemovedAndReaddedSameUid)
                {
                    Assert.True(sessions.TryRemove(connection, out var removed)); Assert.Same(current, removed);
                    if (change == RuneRankPostSendChange.RemovedAndReaddedSameUid)
                        Assert.Throws<InvalidOperationException>(() => sessions.Set(connection,
                            current with { Character = current.Character with { Name = "RetiredRankOwner" } }));
                }
                else if (change == RuneRankPostSendChange.DifferentUid)
                    Assert.Throws<InvalidOperationException>(() => sessions.Set(connection,
                        current with { Character = current.Character with { Uid = actor + 99 } }));
                else if (change == RuneRankPostSendChange.DifferentAccountSameUid)
                    Assert.Throws<InvalidOperationException>(() => sessions.Set(connection,
                        current with { AccountId = actor + 99 }));
                else
                    sessions.Set(connection, current with { });
            };
            try { await f.SendAsync(actor, CreateRuneRankingComposeUse()); }
            finally { f.BeforeSend = null; }
            Assert.True(fired); Assert.Equal(2, observed);
            Assert.Equal(firstA + 2, f.Frames(actor).Count); Assert.Equal(firstB, f.Frames(peer).Count);
            Assert.Equal(OwnedComposeRemoveGolden(RuneRankMaterialUid), f.Frames(actor)[firstA]);
            Assert.Equal(OwnedComposeUpdateGolden(expectedTarget), f.Frames(actor)[firstA + 1]);
            Assert.False(sessions.TryUpdateRuneRankingWithNativeIdentity(connection, original, identity, owner));
            Assert.Equal(new LegacyRuneRankingEntry(actor, original.Character.Name, 10_300),
                Assert.Single(owner.Snapshot(), row => row.CharacterUid == actor));
            Assert.Null(owner.FindRank(actor + 99));
            f.Data.AssertFiles(files);
            Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor)));
            Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems));
            AssertOwnedComposeUidRetirement(originalBytes, expectedBytes, [RuneRankMaterialUid]);
            if (change is RuneRankPostSendChange.Removed or RuneRankPostSendChange.RemovedAndReaddedSameUid)
                Assert.False(sessions.TryGet(connection, out _));
            else
            {
                var current = f.Current(actor);
                Assert.Equal(actor, current.AccountId); Assert.Equal(actor, current.Character.Uid);
                Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
                AssertOwnedComposeConverged(f, actor, files);
                if (change == RuneRankPostSendChange.SameUid)
                {
                    Assert.NotNull(committedAtTarget); Assert.NotSame(committedAtTarget, current);
                    Assert.Equal(original.Character.Name, current.Character.Name);
                    Assert.True(sessions.TryUpdateRuneRankingWithNativeIdentity(connection, current, identity, owner));
                    Assert.Equal(new LegacyRuneRankingEntry(actor, original.Character.Name, 10_300),
                        Assert.Single(owner.Snapshot(), row => row.CharacterUid == actor));
                }
            }
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), f.Frames(peer), peer, rank: 2, score: 10_000);
            AssertOwnedComposeConverged(f, peer, files);
            Assert.Contains("committed ComposeRune", f.Log.ToString(), StringComparison.Ordinal);
        }, 1_293_600, 1_293_601);
    }
}
