using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RelicDecomposeUid = 5_348_800;
    private const uint RelicDecomposeSurplusUid = 5_348_801;
    private const uint RelicDecomposeCrystalId = 3_306_885;

    [Fact]
    public async Task HandlerRelicsDecomposeDeclaredPrefixGrantsNineRowsAndPublishesRank()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.Ordinary);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeIgnoresMissingSurplusOutsideDeclaredPrefix()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.MissingSurplus);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeMissingDeclaredUidRejectsBeforeAllMutation()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.MissingDeclared);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeDuplicateOneUnitIgnoresFailedSecondRemoval()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.DuplicateOne);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeDuplicateTwoUnitSendsUpdateThenRemoval()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.DuplicateTwo);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeMissingCrystalMetadataStillDebitsEchoesAndRanks()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.NoMetadata);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeFullInventoryDoesNotGrantNineRewards()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.Full);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeZeroCountIgnoresTailWithoutCrystalMetadata()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.Zero);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeFiftyCountIgnoresTailWithoutCrystalMetadata()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.Fifty);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeMaximumCountIgnoresTailWithoutCrystalMetadata()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.Maximum);
    }

    [Fact]
    public async Task HandlerRelicsDecomposeEmptyArgsWithCatalogPublishesRankWithoutItemMutation()
    {
        await AssertRelicDecomposeRankingAsync(RelicDecomposeScenario.EmptyWithCatalog);
    }

    private enum RelicDecomposeScenario
    {
        Ordinary, MissingSurplus, MissingDeclared, DuplicateOne, DuplicateTwo,
        NoMetadata, Full, Zero, Fifty, Maximum, EmptyWithCatalog,
    }

    private async Task AssertRelicDecomposeRankingAsync(RelicDecomposeScenario scenario)
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: false);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var fixtureItems = new List<PlayerItemSnapshotLoaderTests.TestDbItem>
        {
            PlayerItemSnapshotLoaderTests.DbItem(RelicDecomposeUid, 4_100_001, position: 0,
                durability: 500, maximumDurability: 500,
                stackSize: scenario == RelicDecomposeScenario.DuplicateTwo ? (ushort)2 : (ushort)1),
            PlayerItemSnapshotLoaderTests.DbItem(RelicDecomposeSurplusUid, 4_100_002, position: 0,
                durability: 500, maximumDurability: 500, stackSize: 1),
        };
        if (scenario == RelicDecomposeScenario.Full)
        {
            fixtureItems.AddRange(Enumerable.Range(0, 38).Select(index =>
                PlayerItemSnapshotLoaderTests.DbItem(
                    checked(5_348_000U + (uint)index), checked(4_100_100U + (uint)index),
                    position: 0, durability: 1, maximumDurability: 1, stackSize: 1)));
        }
        // Canonical physical order: every inventory row BEFORE the two rune rows.
        fixtureItems.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        fixtureItems.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, fixtureItems.ToArray());

        var emptyArgs = scenario is RelicDecomposeScenario.Zero or
            RelicDecomposeScenario.Fifty or RelicDecomposeScenario.Maximum or RelicDecomposeScenario.EmptyWithCatalog;
        var missingDeclared = scenario == RelicDecomposeScenario.MissingDeclared;
        var hasMetadata = scenario == RelicDecomposeScenario.EmptyWithCatalog ||
            (!emptyArgs && scenario != RelicDecomposeScenario.NoMetadata);
        var catalog = CreateRelicDecomposeRankingCatalog(hasMetadata);
        Assert.Equal(hasMetadata, catalog.ItemsById.ContainsKey(checked((int)RelicDecomposeCrystalId)));
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
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(scenario == RelicDecomposeScenario.Full ? 40 : 2, originalA.InventoryItems.Count);
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));

        const uint missingUid = 5_349_999;
        Assert.DoesNotContain(originalA.InventoryItems, item => item.Uid == missingUid);
        Assert.DoesNotContain(originalA.InventoryItems, item => item.ItemId == RelicDecomposeUid);
        var duplicate = scenario is RelicDecomposeScenario.DuplicateOne or RelicDecomposeScenario.DuplicateTwo;
        uint declared = scenario switch
        {
            RelicDecomposeScenario.Zero or RelicDecomposeScenario.EmptyWithCatalog => 0,
            RelicDecomposeScenario.Fifty => 50,
            RelicDecomposeScenario.Maximum => uint.MaxValue,
            RelicDecomposeScenario.MissingDeclared => 2,
            _ => duplicate ? 2U : 1U,
        };
        uint[] physical = scenario switch
        {
            RelicDecomposeScenario.EmptyWithCatalog => [],
            RelicDecomposeScenario.Ordinary => [RelicDecomposeUid, RelicDecomposeSurplusUid],
            RelicDecomposeScenario.MissingSurplus or RelicDecomposeScenario.MissingDeclared =>
                [RelicDecomposeUid, missingUid],
            _ => duplicate ? [RelicDecomposeUid, RelicDecomposeUid] : [RelicDecomposeUid],
        };
        var request = CreateRelicDecomposeCountRequest(declared, physical);
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        var expectedRewards = missingDeclared || emptyArgs || !hasMetadata ||
            scenario == RelicDecomposeScenario.Full ? 0 : duplicate ? 18 : 9;
        var expectedDebits = missingDeclared || emptyArgs ? 0 :
            scenario == RelicDecomposeScenario.DuplicateTwo ? 2 : 1;
        Assert.Equal(beforeA + expectedDebits + expectedRewards + (missingDeclared ? 0 : 1), sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        var currentSessionA = GetSession(handler, connectionA);
        var currentA = currentSessionA.PlayerItems;
        if (missingDeclared)
        {
            Assert.Same(originalSessionA, currentSessionA);
            Assert.Same(originalA, currentA);
        }
        Assert.Equal(originalSessionA with { PlayerItems = currentA }, currentSessionA);
        var rewardRows = currentA.InventoryItems.Where(item => item.ItemId == RelicDecomposeCrystalId).ToArray();
        Assert.Equal(expectedRewards, rewardRows.Length);
        Assert.Equal(expectedRewards, rewardRows.Select(item => item.Uid).Distinct().Count());
        var expectedRows = new List<PlayerItemSnapshotItem>();
        foreach (var reward in rewardRows)
        {
            Assert.DoesNotContain(originalA.InventoryItems.Concat(originalA.RuneItems),
                original => original.Uid == reward.Uid);
            var expected = new PlayerItemSnapshotItem
            {
                Uid = reward.Uid, ItemId = RelicDecomposeCrystalId,
                Position = 0, StackSize = 1, Durability = 1, MaximumDurability = 1,
            };
            expectedRows.Add(expected);
        }
        var survivors = missingDeclared || emptyArgs ? originalA.InventoryItems :
            originalA.InventoryItems.Where(item => item.Uid != RelicDecomposeUid).ToArray();
        var expectedA = originalA with { InventoryItems = survivors.Concat(expectedRows).ToArray() };
        var expectedABytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(currentA));
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(currentA));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));

        var cursor = beforeA;
        if (expectedDebits != 0)
        {
            if (scenario == RelicDecomposeScenario.DuplicateTwo)
            {
                var firstDebit = Assert.Single(originalA.InventoryItems,
                    item => item.Uid == RelicDecomposeUid).WithStackSize(1);
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(
                    firstDebit, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), sentA[cursor++]);
            }
            else
            {
                AssertItemUsage(sentA[cursor++], new MsgItemUsagePacket(
                    RelicDecomposeUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
            }
        }
        for (var index = 0; index < expectedRows.Count; index++)
        {
            if (scenario == RelicDecomposeScenario.DuplicateTwo && index == 9)
            {
                AssertItemUsage(sentA[cursor++], new MsgItemUsagePacket(
                    RelicDecomposeUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
            }
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(
                expectedRows[index], mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes(), sentA[cursor++]);
        }
        if (!missingDeclared)
        {
            uint[] selectedArgs = emptyArgs ? [] : duplicate ?
                [RelicDecomposeUid, RelicDecomposeUid] : [RelicDecomposeUid];
            var echo = CreateRelicDecomposeCountRequest(declared, selectedArgs);
            Assert.Equal(echo, sentA[cursor++]);
            Assert.Equal(MsgItemUsagePacket.PayloadLength + selectedArgs.Length * sizeof(uint) + 4, echo.Length);
            AssertItemUsage(echo, new MsgItemUsagePacket(
                0, 0, MsgItemUsageAction.DecomposeRelics, 511_043_495, declared, 0, 0));
        }
        Assert.Equal(sentA.Count, cursor);
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB)));

        // B FIRST: querying A first would repair the stale row and mask an absent/forbidden hook.
        await AssertRuneRankingQueryAsync(handler, connectionB, sentB, RuneRankPlayerB,
            rank: missingDeclared ? 2 : 1, score: 10_000);
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains(missingDeclared ? "skipped MsgItemUsage decompose relics" :
            "handled MsgItemUsage decompose relics", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage decompose relics", log.ToString());
    }

    private static byte[] CreateRelicDecomposeCountRequest(uint declared, params uint[] physical)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + physical.Length * sizeof(uint));
        writer.WriteUInt32(0, 0);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRelics);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, declared);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < physical.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), physical[index]);
        }
        return writer.ToArray();
    }
}
