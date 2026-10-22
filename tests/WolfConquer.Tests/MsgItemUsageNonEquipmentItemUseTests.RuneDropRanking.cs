using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneDropRemovesActiveRuneAndRefreshesSharedRanking()
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 16, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: 1, score: 10_300);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);

        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
        Assert.Equal(2, originalA.RuneItems.Count);
        Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
        var storedRune = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankStoredUid);
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var expectedA = originalA with
        {
            RuneItems = originalA.RuneItems.Where(item => item.Uid != RuneRankTargetUid).ToArray(),
        };
        var expectedABytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        var beforeDropA = sentA.Count;
        var beforeDropB = sentB.Count;

        await handler.HandleFrameAsync(
            connectionA,
            ReadFrame(new MsgItemUsagePacket(
                RuneRankTargetUid, 0, MsgItemUsageAction.DropRune, 0, 0, 0, 0).ToFrameBytes()));

        // DropRune removes privately; ranking updates are cache-only, not extra outgoing text.
        Assert.Equal(beforeDropA + 1, sentA.Count);
        Assert.Equal(beforeDropB, sentB.Count);
        AssertItemUsage(sentA[beforeDropA], new MsgItemUsagePacket(
            RuneRankTargetUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        var changedA = GetSession(handler, connectionA).PlayerItems;
        Assert.DoesNotContain(changedA.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(storedRune, Assert.Single(changedA.RuneItems));
        Assert.Equal(5_000U, LegacyRuneStoragePacket.ComputeStorageScore(changedA));
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(changedA));
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        Assert.Equal(5_000U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));
        Assert.DoesNotContain(reloadedA.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));

        // Query B first: its private rank text precedes its own refresh and must see A's drop.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: 1, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: null, score: 5_000);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("handled MsgItemUsage drop rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage drop rune", log.ToString());
    }

    [Fact]
    public async Task HandlerRuneDropMissingUidRefreshesStaleLoadedRankingWithoutMutation()
    {
        const uint missingUid = 5_348_999;
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        // The persisted rank score is older than A's live rune state, a valid loaded-history case.
        var catalog = CreateRuneRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
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
        Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == missingUid);
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var beforeDropA = sentA.Count;
        var beforeDropB = sentB.Count;

        await handler.HandleFrameAsync(
            connectionA,
            ReadFrame(new MsgItemUsagePacket(
                missingUid, 0, MsgItemUsageAction.DropRune, 0, 0, 0, 0).ToFrameBytes()));

        Assert.Equal(beforeDropA, sentA.Count);
        Assert.Equal(beforeDropB, sentB.Count);
        Assert.Same(originalSessionA, GetSession(handler, connectionA));
        Assert.Same(originalA, GetSession(handler, connectionA).PlayerItems);
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));

        // Do not query A before this: a query itself refreshes after publishing its rank text.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: 1, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: null, score: 9_700);
        Assert.Same(originalA, GetSession(handler, connectionA).PlayerItems);
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("skipped MsgItemUsage drop rune", log.ToString());
        Assert.DoesNotContain("handled MsgItemUsage drop rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage drop rune", log.ToString());
    }
}
