using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneChanceZeroExperienceRefreshesStaleSharedRankAfterUpdate()
    {
        await AssertRuneChanceRefreshesStaleSharedRankAsync(targetPresent: true);
    }

    [Fact]
    public async Task HandlerRuneChanceMissingTargetRefreshesStaleSharedRankWithoutMutation()
    {
        await AssertRuneChanceRefreshesStaleSharedRankAsync(targetPresent: false);
    }

    private async Task AssertRuneChanceRefreshesStaleSharedRankAsync(bool targetPresent)
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var source = CreateRuneRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        // These legitimate saved active items have no claim/spell. Calling
        // Equip here would repair A's stale cache before the action under test.
        await WithOwnedRuneComposeAsync(source, async f =>
        {
            const uint actor = (uint)RuneRankPlayerA, peer = (uint)RuneRankPlayerB;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, rank: 1, score: 9_700); AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
            var originalSessionA = f.Current(actor); var originalSessionB = f.Current(peer);
            var originalA = originalSessionA.PlayerItems; var originalB = originalSessionB.PlayerItems;
            var originalRune = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
            Assert.Equal(RuneRankTargetId, originalRune.ItemId); Assert.Equal(0U, originalRune.RuneExperience);
            Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA)); Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
            var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA); var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
            Assert.Equal(originalABytes, File.ReadAllBytes(f.ItemPath(actor))); Assert.Equal(originalBBytes, File.ReadAllBytes(f.ItemPath(peer)));
            var files = f.Data.Files(); int beforeA = sentA.Count, beforeB = sentB.Count;
            const uint missingUid = 5_348_999; Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == missingUid);
            f.BeforeSend = (uid, _) =>
            { Assert.Equal(actor, uid); AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files); };
            try { await f.SendAsync(actor, CreateRuneEquipmentUse(targetPresent ? RuneRankTargetUid : missingUid, 0, MsgItemUsageAction.ChanceRune)); }
            finally { f.BeforeSend = null; }
            // One complete2116 only for a present zero-XP item; B receives none.
            Assert.Equal(beforeA + (targetPresent ? 1 : 0), sentA.Count); Assert.Equal(beforeB, sentB.Count);
            if (targetPresent)
            {
                Assert.Equal(OwnedComposeUpdateGolden(originalRune), sentA[beforeA]);
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sentA[beforeA]), out var update));
                Assert.Equal(RuneRankTargetUid, update.Uid); Assert.Equal(RuneRankTargetId, update.ItemId);
                Assert.Equal(0U, update.RuneExperience); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
                AssertRuneUntouchedSessionFields(originalSessionA, f.Current(actor));
            }
            else Assert.Same(originalSessionA, f.Current(actor));
            Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(actor).PlayerItems));
            Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
            Assert.Same(originalSessionB, f.Current(peer)); Assert.Same(originalB, f.Current(peer).PlayerItems);
            Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
            var reloadedA = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor);
            var reloadedB = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(peer);
            Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA)); Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedB));
            Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA)); Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedB));
            AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files);
            // B first proves the Chance request repaired A, rather than A's query.
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB, rank: 1, score: 10_000);
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(actor), sentA, RuneRankPlayerA, rank: null, score: 9_700);
            Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA)); Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
            // The action's exact peer identity was proved before these separate
            // queries, which may legitimately refresh its outer session.
            AssertRuneUntouchedSessionFields(originalSessionB, f.Current(peer));
            Assert.Same(originalB, f.Current(peer).PlayerItems);
            Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(peer).PlayerItems));
            AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files);
            var output = f.Log.ToString();
            if (targetPresent)
            {
                Assert.Contains("committed ChanceRune", output, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("skipped ChanceRune", output, StringComparison.Ordinal);
                Assert.DoesNotContain("committed ChanceRune", output, StringComparison.Ordinal);
            }
            Assert.DoesNotContain("PARITY-GAP MsgItemUsage chance rune", output, StringComparison.Ordinal);
        }, (uint)RuneRankPlayerA, (uint)RuneRankPlayerB);
    }
}
