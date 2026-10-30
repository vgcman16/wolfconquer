using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneComposePrefixAbortInvalidUid = 5_348_903;
    private const uint RuneComposePrefixAbortInvalidId = 1_000_000;

    [Fact]
    public async Task HandlerRuneComposeInvalidLaterRefusesAtomicallyWithoutItemsOutputOrRankMutation()
    {
        await AssertRuneComposePrefixAbortAsync(RuneComposePrefixAbortCase.InvalidLater);
    }

    [Fact]
    public async Task HandlerRuneComposeInvalidFirstLeavesItemsFilesAndRankUnchanged()
    {
        await AssertRuneComposePrefixAbortAsync(RuneComposePrefixAbortCase.InvalidFirst);
    }

    [Fact]
    public async Task HandlerRuneComposeMissingSecondRejectsBeforeAcceptedPrefixMutation()
    {
        await AssertRuneComposePrefixAbortAsync(RuneComposePrefixAbortCase.MissingSecond);
    }

    private enum RuneComposePrefixAbortCase
    {
        InvalidLater, InvalidFirst, MissingSecond,
    }

    private async Task AssertRuneComposePrefixAbortAsync(RuneComposePrefixAbortCase scenario)
    {
        WriteOwnedRuneComposeRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: false);
        WriteOwnedRuneComposeRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        WriteItems(RuneRankPlayerA,
            PlayerItemSnapshotLoaderTests.DbItem(RuneRankMaterialUid, RuneRankMaterialId, 0, durability: 100, maximumDurability: 100, stackSize: 2250),
            PlayerItemSnapshotLoaderTests.DbItem(RuneComposePrefixAbortInvalidUid, RuneComposePrefixAbortInvalidId, 0, durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneRankTargetId, 102, durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId, 212, durability: 100, maximumDurability: 100, stackSize: 1));
        var catalog = CreateRuneComposePrefixAbortCatalog([RuneRankSeed(RuneRankPlayerB, 10_000)]);
        await WithOwnedRuneComposeAsync(catalog, async f =>
        {
            const uint actor = 1_293_600, peer = 1_293_601; const uint missingUid = 5_988_999;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, rank: null, score: 9_700); AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);
            var originalA = f.Current(actor); var originalB = f.Current(peer);
            var originalABytes = File.ReadAllBytes(f.ItemPath(actor)); var originalBBytes = File.ReadAllBytes(f.ItemPath(peer));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems), originalABytes);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems), originalBBytes);
            Assert.DoesNotContain(originalA.PlayerItems.InventoryItems.Concat(originalA.PlayerItems.RuneItems), row => row.Uid == missingUid);
            Assert.Single(originalA.PlayerItems.InventoryItems, row => row.Uid == RuneRankMaterialUid && row.StackSize == 2250);
            Assert.Single(originalA.PlayerItems.InventoryItems, row => row.Uid == RuneComposePrefixAbortInvalidUid && row.ItemId == RuneComposePrefixAbortInvalidId);
            uint[] selected = scenario switch
            {
                RuneComposePrefixAbortCase.InvalidFirst => [RuneComposePrefixAbortInvalidUid, RuneRankMaterialUid],
                RuneComposePrefixAbortCase.MissingSecond => [RuneRankMaterialUid, missingUid],
                _ => [RuneRankMaterialUid, RuneComposePrefixAbortInvalidUid],
            };
            var files = f.Data.Files(); int firstA = sentA.Count, firstB = sentB.Count;
            AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files);
            await f.SendAsync(actor, CreateRuneComposePrefixAbortUse(selected));
            Assert.Equal(firstA, sentA.Count); Assert.Equal(firstB, sentB.Count);
            Assert.Same(originalA, f.Current(actor)); Assert.Same(originalA.PlayerItems, f.Current(actor).PlayerItems);
            Assert.Same(originalB, f.Current(peer)); Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems);
            AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files);
            var reloadedA = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor);
            Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
            Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));
            Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(peer)));
            // Query B first: the rejected request cannot insert a higher A score.
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB, rank: 1, score: 10_000);
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(actor), sentA, RuneRankPlayerA, rank: null, score: 9_700);
            f.Data.AssertFiles(files); Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems));
            // The separate equipment queries may publish a fresh outer session;
            // rejection identity was already proved before those queries.
            AssertRuneUntouchedSessionFields(originalB, f.Current(peer));
            Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems);
            Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems));
            AssertOwnedComposeConverged(f, actor, files); AssertOwnedComposeConverged(f, peer, files);
            Assert.DoesNotContain("committed ComposeRune", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("persisted MsgItemUsage compose rune accepted prefix", f.Log.ToString(), StringComparison.Ordinal);
        }, 1_293_600, 1_293_601);
    }

    private static byte[] CreateRuneComposePrefixAbortUse(params uint[] selected)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + selected.Length * sizeof(uint));
        writer.WriteUInt32(0, RuneRankTargetUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.ComposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, checked((uint)selected.Length));
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < selected.Length; index++)
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), selected[index]);
        return writer.ToArray();
    }
}
