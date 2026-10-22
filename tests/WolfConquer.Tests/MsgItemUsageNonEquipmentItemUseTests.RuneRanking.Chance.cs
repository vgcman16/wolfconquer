using WolfConquer.Core.Accounts;
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
        var catalog = CreateRuneRankingCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);

        // Startup reads the coherent but stale table; it must not eagerly refresh A's live 9700.
        AssertRuneRankingStartup(sentA, rank: 1, score: 9_700);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var originalRune = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(RuneRankTargetId, originalRune.ItemId);
        Assert.Equal(0U, originalRune.RuneExperience);
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        // The physical baseline is AFTER world entry and its ordinary callback persistence.
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        const uint missingUid = 5_348_999;
        Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == missingUid);

        await handler.HandleFrameAsync(
            connectionA,
            ReadFrame(CreateRuneEquipmentUse(
                targetPresent ? RuneRankTargetUid : missingUid,
                dwParam: 0,
                MsgItemUsageAction.ChanceRune)));

        // Neither branch sends an immediate rank packet or sends anything to B.
        Assert.Equal(beforeA + (targetPresent ? 1 : 0), sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        if (targetPresent)
        {
            var expectedUpdate = LegacyGameItemPacket.FromSnapshotItem(
                originalRune,
                LegacyGameItemPacket.CreateRuneSpellOverride(originalRune, catalog.ItemsById),
                LegacyGameItemPacket.UpdateItemMode);
            Assert.Equal(expectedUpdate.ToFrameBytes(), sentA[beforeA]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sentA[beforeA]), out var update));
            Assert.Equal(RuneRankTargetUid, update.Uid);
            Assert.Equal(RuneRankTargetId, update.ItemId);
            Assert.Equal(0U, update.RuneExperience);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
            Assert.Same(originalSessionA.Character, GetSession(handler, connectionA).Character);
        }
        else
        {
            Assert.Same(originalSessionA, GetSession(handler, connectionA));
        }

        var afterRequestA = GetSession(handler, connectionA).PlayerItems;
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(afterRequestA));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        var reloadedB = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedB));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedB));

        // B's first later query is the witness: its own refresh cannot repair A's stale row.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: 1, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: null, score: 9_700);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            GetSession(handler, connectionA).PlayerItems));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            GetSession(handler, connectionB).PlayerItems));
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var output = log.ToString();
        if (targetPresent)
        {
            Assert.Contains("handled MsgItemUsage chance rune", output);
            Assert.Contains("chanceRate=0", output);
            Assert.Contains("chancePassed=False", output);
            Assert.Contains("persisted MsgItemUsage chance rune items", output);
        }
        else
        {
            Assert.Contains("skipped MsgItemUsage chance rune", output);
            Assert.DoesNotContain("handled MsgItemUsage chance rune", output);
        }
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage chance rune", output);
    }
}
