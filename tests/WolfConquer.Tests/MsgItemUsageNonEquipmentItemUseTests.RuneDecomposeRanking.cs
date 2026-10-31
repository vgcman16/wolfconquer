using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneDecomposeEssenceSuccessRefreshesRankAndReloadsPhysicalItems()
    {
        WriteRuneDecomposeRankPlayerA(targetLevel: 16, inventoryCount: 0, bagCount: 0);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneDecomposeRankCatalog([RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        await WithOwnedLegacyDecomposeAsync(catalog, async f =>
        {
            const uint actor = (uint)RuneRankPlayerA, peer = (uint)RuneRankPlayerB;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, 1, 10_300); AssertRuneRankingStartup(sentB, 2, 10_000);
            var originalA = f.Current(actor).PlayerItems; var originalB = f.Current(peer);
            var oldABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA); var oldBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems);
            Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(originalA)); int beforeB = sentB.Count;
            // Actual cumulative402 L16=80500 gives6440, replacing the old
            // synthetic600=>48 quantity while retaining rank10300->5000.
            var expected = await CommitOwnedLegacyDecompose(f, actor, RuneRankTargetUid, 6440, CreateRuneDecomposeRankUse(1, RuneRankTargetUid));
            Assert.Equal((ushort)6440, expected.Essence.StackSize); Assert.Equal(5_000U, LegacyRuneStoragePacket.ComputeStorageScore(f.Current(actor).PlayerItems));
            Assert.Equal(oldABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
            Assert.Same(originalB, f.Current(peer)); Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems);
            Assert.Equal(beforeB, sentB.Count); Assert.Equal(oldBBytes, File.ReadAllBytes(f.ItemPath(peer)));
            var ledger = File.ReadAllBytes(f.Data.State);
            var spellA = File.ReadAllBytes(Path.Combine(f.Data.Spells, actor + ".bin")); var spellB = File.ReadAllBytes(Path.Combine(f.Data.Spells, peer + ".bin"));
            // B first witnesses the actual committed shared-cache update.
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB, rank: 1, score: 10_000);
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(actor), sentA, RuneRankPlayerA, rank: null, score: 5_000);
            AssertOwnedLegacyDecomposeAfterQuery(f, actor, expected.Items, spellA, ledger);
            AssertOwnedLegacyDecomposeAfterQuery(f, peer, oldBBytes, spellB, ledger);
            Assert.Equal(oldABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA)); Assert.Equal(oldBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems));
        }, (uint)RuneRankPlayerA, (uint)RuneRankPlayerB);
    }

    [Fact]
    public Task HandlerRuneDecomposeMissingUidDoesNotPublishStaleLoadedRank() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(1, 5_888_999));

    [Fact]
    public Task HandlerRuneDecomposeUnavailableFragmentSlotDoesNotPublishStaleLoadedRank() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(1, RuneRankTargetUid), inventoryCount: 40);

    [Fact]
    public Task HandlerRuneDecomposeFullEssenceBagRefusesBeforeConsumptionOrReservation() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(1, RuneRankTargetUid), targetLevel: 16, bagCount: 100);

    [Fact]
    public Task HandlerRuneDecomposeZeroCountRetainsManagedFailClosedWithoutRankPublication() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(0, RuneRankTargetUid));

    [Fact]
    public Task HandlerRuneDecomposeFiftyCountRetainsManagedFailClosedWithoutRankPublication() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(50, RuneRankTargetUid));

    [Fact]
    public Task HandlerRuneDecomposeMaxCountRetainsManagedFailClosedWithoutRankPublication() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(uint.MaxValue, RuneRankTargetUid));

    [Fact]
    public Task HandlerRuneDecomposeShortDeclaredPrefixFailsClosedWithoutRankPublication() =>
        AssertOwnedRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(2, RuneRankTargetUid));

    private async Task AssertOwnedRuneDecomposeRankRejectedAsync(byte[] request, uint targetLevel = 14, int inventoryCount = 0, int bagCount = 0)
    {
        WriteRuneDecomposeRankPlayerA(targetLevel, inventoryCount, bagCount);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneDecomposeRankCatalog([RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        await WithOwnedLegacyDecomposeAsync(catalog, async f =>
        {
            const uint actor = (uint)RuneRankPlayerA, peer = (uint)RuneRankPlayerB;
            var sentA = f.Frames(actor); var sentB = f.Frames(peer);
            AssertRuneRankingStartup(sentA, 1, targetLevel == 16 ? 10_300U : 9_700U); AssertRuneRankingStartup(sentB, 2, 10_000);
            var originalA = f.Current(actor); var originalB = f.Current(peer);
            var bytesA = PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems); var bytesB = PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems);
            int beforeB = sentB.Count; await RefuseOwnedLegacyDecompose(f, actor, request);
            Assert.Same(originalA, f.Current(actor)); Assert.Same(originalA.PlayerItems, f.Current(actor).PlayerItems);
            Assert.Same(originalB, f.Current(peer)); Assert.Same(originalB.PlayerItems, f.Current(peer).PlayerItems); Assert.Equal(beforeB, sentB.Count);
            Assert.Equal(bytesA, File.ReadAllBytes(f.ItemPath(actor))); Assert.Equal(bytesB, File.ReadAllBytes(f.ItemPath(peer)));
            var ledger = File.ReadAllBytes(f.Data.State);
            var spellA = File.ReadAllBytes(Path.Combine(f.Data.Spells, actor + ".bin")); var spellB = File.ReadAllBytes(Path.Combine(f.Data.Spells, peer + ".bin"));
            await AssertRuneRankingQueryAsync(f.Handler, f.Connection(peer), sentB, RuneRankPlayerB, rank: 2, score: 10_000);
            AssertOwnedLegacyDecomposeAfterQuery(f, actor, bytesA, spellA, ledger); AssertOwnedLegacyDecomposeAfterQuery(f, peer, bytesB, spellB, ledger);
            Assert.Equal(bytesA, PlayerItemSnapshotLoader.ToLegacyBytes(originalA.PlayerItems)); Assert.Equal(bytesB, PlayerItemSnapshotLoader.ToLegacyBytes(originalB.PlayerItems));
            Assert.Contains("held DecomposeRune", f.Log.ToString(), StringComparison.Ordinal);
        }, (uint)RuneRankPlayerA, (uint)RuneRankPlayerB);
    }

    private void WriteRuneDecomposeRankPlayerA(uint targetLevel, int inventoryCount, int bagCount)
    {
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1_015, x: 826, y: 565);
        var items = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        for (int index = 0; index < inventoryCount; index++) items.Add(PlayerItemSnapshotLoaderTests.DbItem(5_400_000U + (uint)index,
            RuneRankMaterialId, position: 0, durability: 100, maximumDurability: 100, stackSize: 1));
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneRankTargetId - 14 + targetLevel, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        for (int index = 0; index < bagCount; index++) items.Add(PlayerItemSnapshotLoaderTests.DbItem(5_500_000U + (uint)index,
            RuneRankMaterialId, position: 211, durability: 1, maximumDurability: 1, stackSize: 1));
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, items.ToArray());
    }

    private static byte[] CreateRuneDecomposeRankUse(uint declaredCount, params uint[] args)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + args.Length * sizeof(uint));
        writer.WriteUInt32(0, 9_999_999); writer.WriteUInt64(4, 0); writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRune);
        writer.WriteUInt32(14, 511_043_495); writer.WriteUInt32(18, declaredCount); writer.WriteUInt32(22, 0); writer.WriteUInt32(26, 0);
        for (int index = 0; index < args.Length; index++) writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), args[index]);
        var bytes = writer.ToArray(); AssertOwnedLegacyDecomposeRequestCodec(bytes, 9_999_999, declaredCount, args); return bytes;
    }
}
