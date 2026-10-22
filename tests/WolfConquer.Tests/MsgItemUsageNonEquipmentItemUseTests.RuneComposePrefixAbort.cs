using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneComposePrefixAbortInvalidUid = 5_348_903;
    private const uint RuneComposePrefixAbortInvalidId = 1_000_000;

    [Fact]
    public async Task HandlerRuneComposeInvalidLaterRetainsAcceptedPrefixWithoutFinalUpdateOrRank()
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
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        // Canonical physical order: both inventory materials, then counted102 and stored212.
        WriteItems(RuneRankPlayerA,
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneRankMaterialUid, RuneRankMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixAbortInvalidUid, RuneComposePrefixAbortInvalidId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneRankTargetUid, RuneRankTargetId, position: 102,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneRankStoredUid, RuneRankStoredId, position: 212,
                durability: 100, maximumDurability: 100, stackSize: 1));
        var catalog = CreateRuneComposePrefixAbortCatalog([RuneRankSeed(RuneRankPlayerB, 10_000)]);
        Assert.True(catalog.ItemsById.ContainsKey(checked((int)RuneRankMaterialId)));
        Assert.True(catalog.ItemsById.ContainsKey(checked((int)RuneComposePrefixAbortInvalidId)));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: null, score: 9_700);
        AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);
        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        var target = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
        var invalid = Assert.Single(originalA.InventoryItems,
            item => item.Uid == RuneComposePrefixAbortInvalidUid);
        const uint missingUid = 5_988_999;
        Assert.DoesNotContain(originalA.InventoryItems.Concat(originalA.RuneItems),
            item => item.Uid == missingUid);
        uint[] selected = scenario switch
        {
            RuneComposePrefixAbortCase.InvalidFirst => [RuneComposePrefixAbortInvalidUid, RuneRankMaterialUid],
            RuneComposePrefixAbortCase.MissingSecond => [RuneRankMaterialUid, missingUid],
            _ => [RuneRankMaterialUid, RuneComposePrefixAbortInvalidUid],
        };
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(CreateRuneComposePrefixAbortUse(selected)));

        var accepted = scenario == RuneComposePrefixAbortCase.InvalidLater;
        Assert.Equal(beforeA + (accepted ? 1 : 0), sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        if (accepted)
        {
            // Only the already accepted material removal: no final target UPDATE or rank text.
            AssertItemUsage(sentA[beforeA], new MsgItemUsagePacket(
                RuneRankMaterialUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        }
        var expectedTarget = accepted
            ? target.WithItemId(RuneRankTargetId + 2).WithRuneExperience(0)
            : target;
        var expectedA = accepted
            ? originalA with
            {
                InventoryItems = originalA.InventoryItems.Where(item => item.Uid != RuneRankMaterialUid).ToArray(),
                RuneItems = originalA.RuneItems.Select(item =>
                    item.Uid == RuneRankTargetUid ? expectedTarget : item).ToArray(),
            }
            : originalA;
        var currentSessionA = GetSession(handler, connectionA);
        var currentA = currentSessionA.PlayerItems;
        Assert.Equal(originalSessionA with { PlayerItems = currentA }, currentSessionA);
        if (!accepted)
        {
            Assert.Same(originalSessionA, currentSessionA);
            Assert.Same(originalA, currentA);
        }
        var currentTarget = Assert.Single(currentA.RuneItems,
            item => item.Uid == RuneRankTargetUid);
        Assert.Equal(expectedTarget.ItemId, currentTarget.ItemId);
        Assert.Equal(expectedTarget.RuneExperience, currentTarget.RuneExperience);
        Assert.Equal(invalid, Assert.Single(currentA.InventoryItems,
            item => item.Uid == RuneComposePrefixAbortInvalidUid));
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(currentA));
        Assert.Equal(expectedBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        var expectedScore = accepted ? 10_300U : 9_700U;
        Assert.Equal(expectedScore, LegacyRuneStoragePacket.ComputeStorageScore(currentA));
        Assert.Equal(expectedScore, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB)));

        // A is absent from the loaded table. Publishing A10300 here would wrongly demote B.
        // Query B FIRST, then A's first text must still be unranked with its live score.
        await AssertRuneRankingQueryAsync(handler, connectionB, sentB, RuneRankPlayerB, rank: 1, score: 10_000);
        await AssertRuneRankingQueryAsync(handler, connectionA, sentA, RuneRankPlayerA, rank: null, score: expectedScore);
        Assert.Equal(expectedBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage compose rune", output);
        Assert.DoesNotContain("handled MsgItemUsage compose rune", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage compose rune", output);
        if (accepted)
            Assert.Contains("persisted MsgItemUsage compose rune accepted prefix items", output);
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
