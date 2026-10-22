using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneBagMergeExistingUid = 5_349_950;
    private const uint RuneBagMergeSecondUid = 5_349_951;
    private const uint RuneBagMergeBagUid = 5_349_952;
    private const uint RuneBagMergeFragmentId = 3_306_366;
    private const uint RuneBagMergeOtherFragmentId = 3_306_368;

    [Fact]
    public async Task HandlerRuneBagReturnMergesExistingFragmentStackWithSpace()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.Free);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMergesExistingFragmentStackAtFortyInventoryRows()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.Full);
    }

    [Fact]
    public async Task HandlerRuneBagReturnDifferentBoundKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.DifferentBoundFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnOverCapacityKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.OverCapacityFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMultipleFittingStacksKeepsMoveFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MultipleFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMissingMetadataKeepsMoveWithoutRankFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MissingMetadataFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnTimedMetadataKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.TimedFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnOtherFamilyKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.OtherFamilyFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnPlusKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.PlusFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMaximumDurabilityMismatchKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MaximumDurabilityFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnPurificationKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.PurificationFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnRefineryKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.RefineryFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnUnlockTimerKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.UnlockTimerFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnMissingUidKeepsNoOpFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.MissingUidFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnWrongPositionKeepsNoOpFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.WrongPositionFree);
    }

    [Fact]
    public async Task HandlerRuneBagReturnIneligibleCharacterKeepsNoOpFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.IneligibleFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnActivationKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.ActivateFull);
    }

    [Fact]
    public async Task HandlerRuneBagReturnExpirationKeepsFullInventoryFallback()
    {
        await AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase.ExpirationFull);
    }

    private enum RuneBagMergeCase
    {
        Free, Full, DifferentBoundFull, OverCapacityFull, MultipleFree, MissingMetadataFree, TimedFull, OtherFamilyFull, PlusFull, MaximumDurabilityFull, PurificationFull, RefineryFull, UnlockTimerFull, MissingUidFree, WrongPositionFree, IneligibleFull, ActivateFull, ExpirationFull,
    }

    private async Task AssertRuneBagExistingStackMergeAsync(RuneBagMergeCase scenario)
    {
        var merge = scenario is RuneBagMergeCase.Free or RuneBagMergeCase.Full;
        var moveFallback = scenario is RuneBagMergeCase.MultipleFree or RuneBagMergeCase.MissingMetadataFree;
        var full = scenario is not (RuneBagMergeCase.Free or RuneBagMergeCase.MultipleFree or
            RuneBagMergeCase.MissingMetadataFree or RuneBagMergeCase.MissingUidFree or
            RuneBagMergeCase.WrongPositionFree);
        var eligible = scenario != RuneBagMergeCase.IneligibleFull;
        var metadata = scenario != RuneBagMergeCase.MissingMetadataFree;
        var itemId = scenario == RuneBagMergeCase.OtherFamilyFull
            ? RuneBagMergeOtherFragmentId : RuneBagMergeFragmentId;
        var bagBound = (byte)(scenario is RuneBagMergeCase.Free or RuneBagMergeCase.DifferentBoundFull ? 1 : 0);
        var existingBound = scenario == RuneBagMergeCase.DifferentBoundFull ? (byte)0 : bagBound;
        var existingStack = scenario == RuneBagMergeCase.OverCapacityFull ? (ushort)9_999 : (ushort)3;
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA,
            mapId: 1_015, x: 826, y: 565, level: eligible ? 140 : 14);
        var existing = PlayerItemSnapshotLoaderTests.DbItem(
            RuneBagMergeExistingUid, itemId, position: 0,
            durability: 1, maximumDurability: 1, stackSize: existingStack,
            locked: 2, bound: existingBound);
        existing = scenario switch
        {
            RuneBagMergeCase.PlusFull => existing with { Plus = 1 },
            RuneBagMergeCase.MaximumDurabilityFull => existing with { MaximumDurability = 2 },
            RuneBagMergeCase.PurificationFull => existing with { PurificationItemId = 2_000 },
            RuneBagMergeCase.RefineryFull => existing with { RefineryEffectId = 2_000 },
            RuneBagMergeCase.UnlockTimerFull => existing with { UnlockTimer = 1 },
            _ => existing,
        };
        var physical = new List<PlayerItemSnapshotLoaderTests.TestDbItem> { existing };
        if (scenario == RuneBagMergeCase.MultipleFree)
            physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
                RuneBagMergeSecondUid, itemId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 4, bound: bagBound));
        if (full)
        {
            var fillerId = itemId == RuneBagMergeFragmentId
                ? RuneBagMergeOtherFragmentId : RuneBagMergeFragmentId;
            for (var index = 0; index < 39; index++)
                physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
                    6_100_000U + (uint)index, fillerId, position: 0,
                    durability: 1, maximumDurability: 1, stackSize: 1));
        }
        // Canonical inventory-first order, then counted102, bag211 (or control212), stored212.
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneBagMergeBagUid, itemId,
            position: (ushort)(scenario == RuneBagMergeCase.WrongPositionFree ? 212 : 211),
            durability: 1, maximumDurability: 1, stackSize: 2, bound: bagBound));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, physical.ToArray());
        if (scenario is RuneBagMergeCase.ActivateFull or RuneBagMergeCase.ExpirationFull)
        {
            // TestDbItem has no clock fields: set only these two first-record fields before entry.
            // DBItem184 offsets44/168 are the real loader/serializer Activate/Expiration slots.
            var fixturePath = RuneRankingItemsPath(RuneRankPlayerA);
            var fixtureBytes = File.ReadAllBytes(fixturePath);
            Assert.Equal(RuneBagMergeExistingUid,
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fixtureBytes.AsSpan(sizeof(int))));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                fixtureBytes.AsSpan(sizeof(int) + 44, sizeof(uint)),
                scenario == RuneBagMergeCase.ActivateFull ? 1U : 0U);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                fixtureBytes.AsSpan(sizeof(int) + 168, sizeof(long)),
                scenario == RuneBagMergeCase.ExpirationFull ? 640_290_528_000_000_000L : 0L);
            File.WriteAllBytes(fixturePath, fixtureBytes);
        }
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneBagMergeCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_000)],
            includeFragment: metadata, fragmentTime: scenario == RuneBagMergeCase.TimedFull ? 1U : 0U);
        if (metadata)
        {
            var fragment = Assert.Single(catalog.ItemsById[(int)RuneBagMergeFragmentId]);
            Assert.Equal("10000", fragment.Fields[46]);
            Assert.Equal(0U, fragment.RequiredClass);
            Assert.Equal(1U, fragment.LegacyDurability);
        }
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        var liveScore = scenario == RuneBagMergeCase.WrongPositionFree ? 30_000U : 9_700U;
        AssertRuneRankingStartup(sentA, rank: 1, score: liveScore);
        AssertRuneRankingStartup(sentB, rank: 2, score: 10_000);
        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        Assert.Equal(full ? 40 : scenario == RuneBagMergeCase.MultipleFree ? 2 : 1,
            originalA.InventoryItems.Count);
        Assert.Equal(liveScore, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        var oldExisting = Assert.Single(originalA.InventoryItems, item => item.Uid == RuneBagMergeExistingUid);
        var oldBag = Assert.Single(originalA.RuneItems, item => item.Uid == RuneBagMergeBagUid);
        Assert.Equal(existingStack, oldExisting.StackSize);
        Assert.Equal((byte)2, oldExisting.Locked);
        if (scenario is RuneBagMergeCase.ActivateFull or RuneBagMergeCase.ExpirationFull)
        {
            Assert.Equal(scenario == RuneBagMergeCase.ActivateFull ? 1U : 0U, oldExisting.Activate);
            Assert.Equal(scenario == RuneBagMergeCase.ExpirationFull ? 640_290_528_000_000_000L : 0L,
                oldExisting.Expiration);
        }
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalBBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        const uint missingUid = 5_349_999;
        Assert.DoesNotContain(originalA.InventoryItems.Concat(originalA.RuneItems), item => item.Uid == missingUid);
        var request = CreateRuneBagUse(
            scenario == RuneBagMergeCase.MissingUidFree ? missingUid : RuneBagMergeBagUid, dwParam: 1);
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        Assert.Equal(beforeB, sentB.Count);
        var outgoing = sentA.Skip(beforeA).ToArray();
        Assert.Equal(merge || moveFallback ? 3 : 0, outgoing.Length);
        var expectedExisting = merge ? oldExisting.WithStackSize(5).WithLocked(0) : oldExisting;
        var expectedA = merge
            ? originalA with
            {
                InventoryItems = originalA.InventoryItems.Select(item =>
                    item.Uid == RuneBagMergeExistingUid ? expectedExisting : item).ToArray(),
                RuneItems = originalA.RuneItems.Where(item => item.Uid != RuneBagMergeBagUid).ToArray(),
            }
            : moveFallback ? originalA with
            {
                InventoryItems = originalA.InventoryItems.Append(oldBag.WithPosition(0)).ToArray(),
                RuneItems = originalA.RuneItems.Where(item => item.Uid != RuneBagMergeBagUid).ToArray(),
            } : originalA;
        var currentSessionA = GetSession(handler, connectionA);
        var currentA = currentSessionA.PlayerItems;
        var currentExisting = Assert.Single(currentA.InventoryItems, item => item.Uid == RuneBagMergeExistingUid);
        Assert.Equal(expectedExisting.StackSize, currentExisting.StackSize);
        Assert.Equal(expectedExisting.Locked, currentExisting.Locked);
        Assert.Equal(expectedExisting.Bound, currentExisting.Bound);
        Assert.Equal(expectedExisting.Plus, currentExisting.Plus);
        if (merge || moveFallback)
        {
            AssertItemUsage(outgoing[0], new MsgItemUsagePacket(
                RuneBagMergeBagUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
            var sentItem = merge ? expectedExisting : oldBag.WithPosition(0);
            var expectedItemFrame = LegacyGameItemPacket.FromSnapshotItem(
                sentItem, mode: merge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode)
                .ToFrameBytes();
            Assert.Equal(expectedItemFrame, outgoing[1]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(outgoing[1]), out var itemPacket));
            Assert.Equal(merge ? RuneBagMergeExistingUid : RuneBagMergeBagUid, itemPacket.Uid);
            Assert.Equal(merge ? (ushort)5 : (ushort)2, itemPacket.StackSize);
            Assert.Equal((byte)0, itemPacket.Position);
            Assert.Equal(request, outgoing[2]);
            Assert.DoesNotContain(currentA.RuneItems, item => item.Uid == RuneBagMergeBagUid);
            if (merge)
            {
                Assert.DoesNotContain(currentA.InventoryItems, item => item.Uid == RuneBagMergeBagUid);
                Assert.Equal(originalA.InventoryItems.Count, currentA.InventoryItems.Count);
                Assert.Equal((byte)0, currentExisting.Locked);
                Assert.Equal(bagBound, currentExisting.Bound);
            }
        }
        else
        {
            Assert.Same(originalSessionA, currentSessionA);
            Assert.Same(originalA, currentA);
        }
        var expectedABytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedA);
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(currentA));
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedABytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB)));
        var publishesRank = merge || (moveFallback && metadata);
        // B FIRST exposes stale A10300 if the merge omitted its rank hook.
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: publishesRank ? 1 : 2, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: publishesRank ? null : 1, score: liveScore);
        Assert.Equal(expectedABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(originalBBytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        var output = log.ToString();
        if (merge)
        {
            Assert.Contains("handled MsgItemUsage direct rune bag existing stack merge", output);
            Assert.Contains("persisted MsgItemUsage direct rune bag existing stack merge items", output);
        }
        else
            Assert.DoesNotContain("existing stack merge", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune bag", output);
    }
}
