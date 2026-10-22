using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneDecomposeEssenceSuccessRefreshesRankAndReloadsPhysicalItems()
    {
        using var log = new StringWriter();
        var env = await EnterRuneDecomposeRankPlayersAsync(log, targetLevel: 16);
        var originalSessionA = GetSession(env.Handler, env.A);
        var originalSessionB = GetSession(env.Handler, env.B);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        var storedRune = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankStoredUid);
        var beforeA = env.SentA.Count;
        var beforeB = env.SentB.Count;

        await env.Handler.HandleFrameAsync(
            env.A, ReadFrame(CreateRuneDecomposeRankUse(1, RuneRankTargetUid)));

        Assert.Equal(beforeA + 3, env.SentA.Count);
        Assert.Equal(beforeB, env.SentB.Count);
        AssertItemUsage(env.SentA[beforeA], new MsgItemUsagePacket(
            RuneRankTargetUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        var changed = GetSession(env.Handler, env.A).PlayerItems;
        Assert.DoesNotContain(changed.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(storedRune, Assert.Single(changed.RuneItems, item => item.Position == 212));
        var essence = Assert.Single(changed.RuneItems, item => item.Position == 211);
        Assert.Equal(RuneRankMaterialId, essence.ItemId);
        Assert.Equal((ushort)48, essence.StackSize); // 600 / 1.25 / 10.
        Assert.Equal((ushort)1, essence.Durability);
        Assert.Equal((ushort)1, essence.MaximumDurability);
        Assert.Equal((byte)0, essence.Bound);
        Assert.Equal(
            LegacyGameItemPacket.FromSnapshotItem(
                essence, LegacyGameItemPacket.CreateRuneSpellOverride(essence, env.Catalog.ItemsById))
                .ToFrameBytes(),
            env.SentA[beforeA + 1]);
        var fragment = Assert.Single(changed.InventoryItems);
        Assert.Equal(RuneDecomposeRankFragmentId, fragment.ItemId);
        Assert.Equal((ushort)10, fragment.StackSize);
        Assert.Equal((byte)1, fragment.Bound);
        Assert.Equal((ushort)0, fragment.Position);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(fragment).ToFrameBytes(), env.SentA[beforeA + 2]);
        Assert.Equal(5_000U, LegacyRuneStoragePacket.ComputeStorageScore(changed));
        AssertRuneDecomposeRankPhysicalReload(changed);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalSessionB, GetSession(env.Handler, env.B));
        Assert.Same(originalB, GetSession(env.Handler, env.B).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));

        // B's text is emitted before its own update; never query A first to mask this hook.
        await AssertRuneRankingQueryAsync(env.Handler, env.B, env.SentB, RuneRankPlayerB, rank: 1, score: 10_000);
        await AssertRuneRankingQueryAsync(env.Handler, env.A, env.SentA, RuneRankPlayerA, rank: null, score: 5_000);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("handled MsgItemUsage decompose rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage decompose rune", log.ToString());
    }

    [Fact]
    public async Task HandlerRuneDecomposeMissingUidDoesNotPublishStaleLoadedRank()
    {
        await AssertRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(1, 5_888_999));
    }

    [Fact]
    public async Task HandlerRuneDecomposeInsufficientInventorySpaceDoesNotPublishStaleLoadedRank()
    {
        await AssertRuneDecomposeRankRejectedAsync(
            CreateRuneDecomposeRankUse(1, RuneRankTargetUid), inventoryCount: 31, expectSpaceDialog: true);
    }

    [Fact]
    public async Task HandlerRuneDecomposeFullEssenceBagDoesNotPublishAfterFailedAdd()
    {
        using var log = new StringWriter();
        var env = await EnterRuneDecomposeRankPlayersAsync(log, targetLevel: 16, bagCount: 100);
        var originalA = GetSession(env.Handler, env.A).PlayerItems;
        var originalB = GetSession(env.Handler, env.B).PlayerItems;
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var originalBag = originalA.RuneItems.Where(item => item.Position == 211).ToArray();
        Assert.Equal(100, originalBag.Length);
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        var beforeA = env.SentA.Count;
        var beforeB = env.SentB.Count;

        await env.Handler.HandleFrameAsync(
            env.A, ReadFrame(CreateRuneDecomposeRankUse(1, RuneRankTargetUid)));

        // Removing equipped102 does not free a Position211 bag slot: no essence Add or rank update.
        Assert.Equal(beforeA + 2, env.SentA.Count);
        Assert.Equal(beforeB, env.SentB.Count);
        AssertItemUsage(env.SentA[beforeA], new MsgItemUsagePacket(
            RuneRankTargetUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        var changed = GetSession(env.Handler, env.A).PlayerItems;
        Assert.DoesNotContain(changed.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(101, changed.RuneItems.Count);
        Assert.Equal(originalBag, changed.RuneItems.Where(item => item.Position == 211).ToArray());
        Assert.Equal(
            Assert.Single(originalA.RuneItems, item => item.Position == 212),
            Assert.Single(changed.RuneItems, item => item.Position == 212));
        var fragment = Assert.Single(changed.InventoryItems);
        Assert.Equal(RuneDecomposeRankFragmentId, fragment.ItemId);
        Assert.Equal((ushort)10, fragment.StackSize);
        Assert.Equal((byte)1, fragment.Bound);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(fragment).ToFrameBytes(), env.SentA[beforeA + 1]);
        Assert.Equal(5_000U, LegacyRuneStoragePacket.ComputeStorageScore(changed));
        AssertRuneDecomposeRankPhysicalReload(changed);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalB, GetSession(env.Handler, env.B).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));

        // Native rank update is gated by Add=true, so B must still see the old cached A10300.
        await AssertRuneRankingQueryAsync(env.Handler, env.B, env.SentB, RuneRankPlayerB, rank: 2, score: 10_000);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("addedEssence=False", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage decompose rune", log.ToString());
    }

    [Fact]
    public async Task HandlerRuneDecomposeZeroCountRetainsManagedFailClosedWithoutRankPublication()
    {
        await AssertRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(0, RuneRankTargetUid));
    }

    [Fact]
    public async Task HandlerRuneDecomposeFiftyCountRetainsManagedFailClosedWithoutRankPublication()
    {
        await AssertRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(50, RuneRankTargetUid));
    }

    [Fact]
    public async Task HandlerRuneDecomposeMaxCountRetainsManagedFailClosedWithoutRankPublication()
    {
        await AssertRuneDecomposeRankRejectedAsync(CreateRuneDecomposeRankUse(uint.MaxValue, RuneRankTargetUid));
    }

    [Fact]
    public async Task HandlerRuneDecomposeShortDeclaredPrefixFailsClosedWithoutRankPublication()
    {
        // Managed safety control only, not proof of the original unsafe short-read outcome.
        await AssertRuneDecomposeRankRejectedAsync(
            CreateRuneDecomposeRankUse(2, RuneRankTargetUid), expectShortLog: true);
    }

    private async Task AssertRuneDecomposeRankRejectedAsync(
        byte[] request, int inventoryCount = 0, bool expectSpaceDialog = false, bool expectShortLog = false)
    {
        using var log = new StringWriter();
        // Live A9700 versus loaded A10300 makes a forbidden rank publication observable.
        var env = await EnterRuneDecomposeRankPlayersAsync(log, targetLevel: 14, inventoryCount: inventoryCount);
        var originalSessionA = GetSession(env.Handler, env.A);
        var originalSessionB = GetSession(env.Handler, env.B);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var beforeA = env.SentA.Count;
        var beforeB = env.SentB.Count;

        await env.Handler.HandleFrameAsync(env.A, ReadFrame(request));

        Assert.Equal(beforeA + (expectSpaceDialog ? 1 : 0), env.SentA.Count);
        Assert.Equal(beforeB, env.SentB.Count);
        if (expectSpaceDialog)
        {
            Assert.Equal(
                LegacyMsgMessagePacket.BoxDialog("Please make 10 free spaces in your inventory.").ToFrameBytes(),
                env.SentA[beforeA]);
        }
        Assert.Same(originalSessionA, GetSession(env.Handler, env.A));
        Assert.Same(originalA, GetSession(env.Handler, env.A).PlayerItems);
        Assert.Same(originalSessionB, GetSession(env.Handler, env.B));
        Assert.Same(originalB, GetSession(env.Handler, env.B).PlayerItems);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        AssertRuneDecomposeRankPhysicalReload(originalA);

        await AssertRuneRankingQueryAsync(env.Handler, env.B, env.SentB, RuneRankPlayerB, rank: 2, score: 10_000);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("skipped MsgItemUsage decompose rune", log.ToString());
        Assert.DoesNotContain("handled MsgItemUsage decompose rune", log.ToString());
        if (expectShortLog)
            Assert.Contains("managed fail-closed", log.ToString());
    }

    private async Task<(
        WorldServerFrameHandler Handler, ConquerTcpConnection A, List<byte[]> SentA,
        ConquerTcpConnection B, List<byte[]> SentB, WorldDataCatalog Catalog)> EnterRuneDecomposeRankPlayersAsync(
        StringWriter log, uint targetLevel, int inventoryCount = 0, int bagCount = 0)
    {
        WriteRuneDecomposeRankPlayerA(targetLevel, inventoryCount, bagCount);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneDecomposeRankCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (a, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (b, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: 1, score: targetLevel == 16 ? 10_300U : 9_700U);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
        return (handler, a, sentA, b, sentB, catalog);
    }

    private void WriteRuneDecomposeRankPlayerA(uint targetLevel, int inventoryCount, int bagCount)
    {
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1_015, x: 826, y: 565);
        var items = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        for (var index = 0; index < inventoryCount; index++)
            items.Add(PlayerItemSnapshotLoaderTests.DbItem(
                checked(5_400_000U + (uint)index), RuneRankMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1));
        // Physical canonical order: inventory, active102, bag211, stored212.
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneRankTargetId - 14 + targetLevel, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        for (var index = 0; index < bagCount; index++)
            items.Add(PlayerItemSnapshotLoaderTests.DbItem(
                checked(5_500_000U + (uint)index), RuneRankMaterialId, position: 211,
                durability: 1, maximumDurability: 1, stackSize: 1));
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, items.ToArray());
    }

    private void AssertRuneDecomposeRankPhysicalReload(PlayerItemSnapshot items)
    {
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        Assert.Equal(bytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        var reloaded = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
        Assert.Equal(
            LegacyRuneStoragePacket.ComputeStorageScore(items),
            LegacyRuneStoragePacket.ComputeStorageScore(reloaded));
    }

    private static byte[] CreateRuneDecomposeRankUse(uint declaredCount, params uint[] args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + (args.Length * sizeof(uint)));
        writer.WriteUInt32(0, 9_999_999);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, declaredCount);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < args.Length; index++)
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        return writer.ToArray();
    }
}
