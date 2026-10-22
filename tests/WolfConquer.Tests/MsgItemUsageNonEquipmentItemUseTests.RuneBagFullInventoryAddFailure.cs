using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneBagFullUid = 5_349_975;
    private const uint RuneBagFullItemId = 4_020_517;

    [Fact]
    public async Task HandlerRuneBagFullInventoryDeletesUnboundRuneWhenNativeAddFails()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.UnboundFull);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryDeletesBoundRuneWhenNativeAddFails()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.BoundFull);
    }

    [Fact]
    public async Task HandlerRuneBagThirtyNineRowsPreservesExistingMoveFallback()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.ThirtyNine);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryOtherRuneKeepsRejectionFallback()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.OtherItem);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryMissingMetadataKeepsRejectionFallback()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.MissingMetadata);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryWrongPositionKeepsRejectionFallback()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.WrongPosition);
    }

    [Fact]
    public async Task HandlerRuneBagFullInventoryUnderAgeKeepsRejectionFallback()
    {
        await AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase.UnderAge);
    }

    private enum RuneBagFullCase
    {
        UnboundFull, BoundFull, ThirtyNine, OtherItem, MissingMetadata, WrongPosition, UnderAge,
    }

    private async Task AssertRuneBagFullInventoryAddFailureAsync(RuneBagFullCase scenario)
    {
        // Deliberately destructive source edge; only disposable owned fixtures may exercise this.
        var losesItem = scenario is RuneBagFullCase.UnboundFull or RuneBagFullCase.BoundFull;
        var moveFallback = scenario == RuneBagFullCase.ThirtyNine;
        var inventoryCount = moveFallback ? 39 : 40;
        var itemId = scenario == RuneBagFullCase.OtherItem ? RuneRankStoredId : RuneBagFullItemId;
        var position = (ushort)(scenario == RuneBagFullCase.WrongPosition ? 212 : 211);
        var bound = (byte)(scenario == RuneBagFullCase.BoundFull ? 1 : 0);
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA,
            mapId: 1_015, x: 826, y: 565, level: scenario == RuneBagFullCase.UnderAge ? 14 : 140);
        var characterPath = Path.Combine(_usersRoot,
            RuneRankPlayerA.ToString(CultureInfo.InvariantCulture) + ".ini");
        var characterText = File.ReadAllText(characterPath);
        Assert.Equal(1, characterText.Split("Class=7051", StringSplitOptions.None).Length - 1);
        // Exact own pre-entry fixture edit: real4020517 Class6000 accepts Monk6000..6051.
        File.WriteAllText(characterPath,
            characterText.Replace("Class=7051", "Class=6000", StringComparison.Ordinal));
        var physical = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        for (var index = 0; index < inventoryCount; index++)
            physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
                6_200_000U + (uint)index, RuneBagMergeFragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 1));
        // Canonical inventory-first, then counted102, bag211/control212, stored212.
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneBagFullUid, itemId, position: position,
            durability: 1, maximumDurability: 1, stackSize: 1, bound: bound));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, physical.ToArray());
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneBagFullInventoryFailureCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)],
            includeReturnedItem: scenario != RuneBagFullCase.MissingMetadata);
        if (scenario != RuneBagFullCase.MissingMetadata)
        {
            var itemType = Assert.Single(catalog.ItemsById[(int)RuneBagFullItemId]);
            Assert.Equal(6_000U, itemType.RequiredClass);
            Assert.Equal("0", itemType.Fields[38]);
            Assert.Equal("0", itemType.Fields[46]);
            Assert.Equal(1U, itemType.LegacyDurability);
        }
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        var liveScore = scenario == RuneBagFullCase.WrongPosition ? 15_300U : 9_700U;
        AssertRuneRankingStartup(sentA, rank: 1, score: liveScore);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        Assert.Equal(6_000L, originalSessionA.Character.Class);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        Assert.Equal(inventoryCount, originalA.InventoryItems.Count);
        Assert.Equal(liveScore, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        var oldBag = Assert.Single(originalA.RuneItems, item => item.Uid == RuneBagFullUid);
        Assert.Equal(itemId, oldBag.ItemId);
        Assert.Equal(position, oldBag.Position);
        Assert.Equal((ushort)1, oldBag.StackSize);
        Assert.Equal(bound, oldBag.Bound);
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var request = CreateRuneBagUse(RuneBagFullUid, dwParam: 1); // Exactly empty args.
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        Assert.Equal(beforeB, sentB.Count);
        var outgoing = sentA.Skip(beforeA).ToArray();
        Assert.Equal(losesItem ? 2 : moveFallback ? 3 : 0, outgoing.Length);
        var expectedA = losesItem
            ? originalA with
            {
                RuneItems = originalA.RuneItems.Where(item => item.Uid != RuneBagFullUid).ToArray(),
            }
            : moveFallback ? originalA with
            {
                InventoryItems = originalA.InventoryItems.Append(oldBag.WithPosition(0)).ToArray(),
                RuneItems = originalA.RuneItems.Where(item => item.Uid != RuneBagFullUid).ToArray(),
            } : originalA;
        var currentSessionA = GetSession(handler, connectionA);
        var currentA = currentSessionA.PlayerItems;
        if (losesItem || moveFallback)
        {
            AssertItemUsage(outgoing[0], new MsgItemUsagePacket(
                RuneBagFullUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
            Assert.Equal(request, outgoing[^1]);
            Assert.DoesNotContain(currentA.RuneItems, item => item.Uid == RuneBagFullUid);
            if (losesItem)
            {
                Assert.Equal(40, currentA.InventoryItems.Count);
                Assert.Same(originalA.InventoryItems, currentA.InventoryItems);
                Assert.DoesNotContain(currentA.InventoryItems, item => item.Uid == RuneBagFullUid);
            }
            else
            {
                var moved = Assert.Single(currentA.InventoryItems, item => item.Uid == RuneBagFullUid);
                Assert.Equal((ushort)0, moved.Position);
                Assert.Equal(itemId, moved.ItemId);
                Assert.Equal(bound, moved.Bound);
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(
                        oldBag.WithPosition(0),
                        LegacyGameItemPacket.CreateRuneSpellOverride(oldBag.WithPosition(0), catalog.ItemsById))
                    .ToFrameBytes(), outgoing[1]);
            }
        }
        else
        {
            Assert.Same(originalSessionA, currentSessionA);
            Assert.Same(originalA, currentA);
        }
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(currentA));
        Assert.Equal(expectedBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalCharacterBytes, File.ReadAllBytes(characterPath));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB)));
        // StaleA10300/live9700: B-FIRST exposes a missing rank update on the selected loss path.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: losesItem || moveFallback ? 1 : 2, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: losesItem || moveFallback ? null : 1, score: liveScore);
        Assert.Equal(expectedBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        if (losesItem)
        {
            Assert.Contains("handled MsgItemUsage direct rune bag full inventory add failure", log.ToString());
            Assert.Contains("persisted MsgItemUsage direct rune bag full inventory add failure items", log.ToString());
        }
        else
            Assert.DoesNotContain("full inventory add failure", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune bag", log.ToString());
    }
}
