using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneUnequipSpellItemId = 4_020_517;
    private const ushort RuneUnequipSpellId = 14_160;

    [Fact]
    public async Task HandlerRuneUnequipRemovesLoadedSpellBeforeEchoAndPersistsBothFiles()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.Present);
    }

    [Fact]
    public async Task HandlerRuneUnequipWithoutMappedLearnedSpellKeepsFiveFrameTail()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.Absent);
    }

    [Fact]
    public async Task HandlerRuneUnequipRemovesMappedIdDespiteDifferentLearnedLevel()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.DifferentLevel);
    }

    [Fact]
    public async Task HandlerRuneUnequipAlreadyStoredRemovesSpellOnceAcrossRepeatedRequests()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.AlreadyStoredRepeat);
    }

    [Fact]
    public async Task HandlerRuneUnequipMissingUidPreservesLoadedSpellAndFiles()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.MissingUid);
    }

    [Fact]
    public async Task HandlerRuneUnequipUnderLevelPreservesLoadedSpellAndFiles()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.UnderLevel);
    }

    [Fact]
    public async Task HandlerRuneUnequipFullInventoryPreservesLoadedSpellAndFiles()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.FullInventory);
    }

    [Fact]
    public async Task HandlerRuneUnequipActiveVendorPreservesLoadedSpellAndFiles()
    {
        await AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase.ActiveVendor);
    }

    private enum RuneUnequipSpellCase
    {
        Present, Absent, DifferentLevel, AlreadyStoredRepeat, MissingUid, UnderLevel, FullInventory, ActiveVendor,
    }

    private async Task AssertRuneUnequipSpellRemovalAsync(RuneUnequipSpellCase scenario)
    {
        var succeeds = scenario is RuneUnequipSpellCase.Present or RuneUnequipSpellCase.Absent
            or RuneUnequipSpellCase.DifferentLevel or RuneUnequipSpellCase.AlreadyStoredRepeat;
        var hasMappedSpell = scenario != RuneUnequipSpellCase.Absent;
        var removesSpell = succeeds && hasMappedSpell;
        var position = (ushort)(scenario == RuneUnequipSpellCase.AlreadyStoredRepeat ? 212 : 102);
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1_015, x: 826, y: 565,
            level: scenario == RuneUnequipSpellCase.UnderLevel ? 14 : 140);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerA);
        var physical = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        if (scenario == RuneUnequipSpellCase.FullInventory)
            for (var index = 0; index < 40; index++)
                physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
                    6_410_000U + (uint)index, 3_306_366, position: 0,
                    durability: 1, maximumDurability: 1, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneUnequipSpellItemId, position: position,
            durability: 1, maximumDurability: 1, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, physical.ToArray());
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerB);

        var unrelated = new PlayerSpellSnapshotEntry(1_000, 2, 9_876, 3, 2, 1);
        var mapped = new PlayerSpellSnapshotEntry(RuneUnequipSpellId,
            (ushort)(scenario == RuneUnequipSpellCase.DifferentLevel ? 15 : 16), 0, 0, 0, 0);
        var initialSpellsA = new PlayerSpellSnapshot(hasMappedSpell ? [mapped, unrelated] : [unrelated]);
        var initialSpellsB = new PlayerSpellSnapshot([mapped with { Experience = 789 }, unrelated]);
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        var initialSpellBytesA = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(initialSpellsA.Spells);
        var initialSpellBytesB = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(initialSpellsB.Spells);
        Assert.Equal((byte)0, initialSpellBytesA[^1]); // Ordinary zero padding, not nonzero-padding parity.
        if (hasMappedSpell)
            Assert.Equal((byte)0, initialSpellBytesA[15]);
        // Only pre-entry fixture files are written. No post-entry session seeding or manual Save.
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerA), initialSpellBytesA);
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerB), initialSpellBytesB);

        // Frozen future599 factory: real4020517 field25=1416016 and real3306366 filler.
        var catalog = CreateRuneBagFullInventoryFailureCatalog(
            [RuneRankSeed(RuneRankPlayerB, 10_000)], includeReturnedItem: true);
        var itemType = Assert.Single(catalog.ItemsById[(int)RuneUnequipSpellItemId]);
        Assert.Equal(6_000U, itemType.RequiredClass);
        Assert.Equal(1_416_016U, itemType.RuneSpellId);
        Assert.Equal(1U, itemType.LegacyDurability);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
        Assert.Equal(spellsRoot, loader.PlayerSpellsRoot);
        var handler = new WorldServerFrameHandler(loader, handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: null, score: 10_600);
        AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);

        if (scenario == RuneUnequipSpellCase.ActiveVendor)
        {
            var startA = sentA.Count;
            var startB = sentB.Count;
            await handler.HandleFrameAsync(connectionA, ReadFrame(new MsgActionPacket
            {
                ObjId = checked((uint)RuneRankPlayerA), Type = MsgActionType.StartVendor,
            }.ToFrameBytes()));
            Assert.Equal(startA + 2, sentA.Count);
            Assert.Equal(startB, sentB.Count);
            Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sentA[startA]), out var booth));
            Assert.Equal(1_393_600U, booth.Uid);
            Assert.Equal(827, booth.X);
            Assert.Equal(565, booth.Y);
            Assert.True(MsgActionPacket.TryParse(ReadFrame(sentA[startA + 1]), out var start));
            Assert.Equal(MsgActionType.StartVendor, start.Type);
            Assert.Equal(booth.Uid, start.DwParam);
            var vending = GetSession(handler, connectionA);
            Assert.Equal("1393600", vending.Character.Attributes["RuntimeVendorUid"]);
            Assert.Same(LegacyVendorListingBook.Empty, vending.LegacyVendorListings);
        }

        var originalSessionA = GetSession(handler, connectionA);
        var originalSessionB = GetSession(handler, connectionB);
        var originalCharacterPath = Path.Combine(originalSessionA.UsersRoot!, originalSessionA.AccountId + ".ini");
        var originalCharacterBytes = File.ReadAllBytes(originalCharacterPath);
        Assert.Equal(6_000L, originalSessionA.Character.Class);
        Assert.Equal(6_000L, originalSessionB.Character.Class);
        Assert.Equal(2L, originalSessionA.Character.Reborn);
        Assert.Equal(scenario == RuneUnequipSpellCase.UnderLevel ? 14L : 140L,
            originalSessionA.Character.Level);
        var originalA = originalSessionA.PlayerItems;
        var originalB = originalSessionB.PlayerItems;
        var originalSpellsA = originalSessionA.PlayerSpells;
        var originalSpellsB = originalSessionB.PlayerSpells;
        Assert.Equal(scenario == RuneUnequipSpellCase.FullInventory ? 40 : 0, originalA.InventoryItems.Count);
        var selected = Assert.Single(originalA.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(RuneUnequipSpellItemId, selected.ItemId);
        Assert.Equal(position, selected.Position);
        Assert.Equal(10_600U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(initialSpellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(originalSpellsA));
        Assert.Equal(initialSpellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(originalSpellsB));
        Assert.Equal(initialSpellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(initialSpellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        Assert.Equal(hasMappedSpell, originalSpellsA.ContainsSpell(RuneUnequipSpellId));
        if (hasMappedSpell)
            Assert.Equal(mapped, Assert.Single(originalSpellsA.Spells, entry => entry.Id == RuneUnequipSpellId));
        Assert.Equal(unrelated, Assert.Single(originalSpellsA.Spells, entry => entry.Id == unrelated.Id));
        var originalItemBytesA = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        var originalItemBytesB = PlayerItemSnapshotLoader.ToLegacyBytes(originalB);
        Assert.Equal(originalItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(originalItemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        var expectedItemsA = succeeds ? originalA with
        {
            RuneItems = originalA.RuneItems.Select(item =>
                item.Uid == RuneRankTargetUid ? item.WithPosition(212) : item).ToArray(),
        } : originalA;
        var expectedItemBytesA = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItemsA);
        var expectedSpellBytesA = initialSpellBytesA;
        if (removesSpell)
        {
            // Independent count1 + untouched original unrelated12B record, not the production serializer.
            expectedSpellBytesA = new byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(expectedSpellBytesA.AsSpan(0, 4), 1);
            initialSpellBytesA.AsSpan(initialSpellBytesA.Length - 12, 12).CopyTo(expectedSpellBytesA.AsSpan(4));
        }
        const uint absentUid = 5_349_999;
        Assert.DoesNotContain(originalA.RuneItems, item => item.Uid == absentUid);
        var request = CreateRuneEquipmentUse(
            scenario == RuneUnequipSpellCase.MissingUid ? absentUid : RuneRankTargetUid,
            dwParam: 102, MsgItemUsageAction.UnequipRune);
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        // First RED boundary: three removal cases require six frames, future600 alone emits five.
        var expectedFrames = succeeds ? removesSpell ? 6 : 5
            : scenario == RuneUnequipSpellCase.MissingUid ? 0 : 1;
        Assert.Equal(beforeA + expectedFrames, sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        var afterSessionA = GetSession(handler, connectionA);
        Assert.Equal(originalCharacterBytes, File.ReadAllBytes(originalCharacterPath));
        if (succeeds)
        {
            var echoIndex = beforeA;
            if (removesSpell)
                AssertRuneUnequipSpellRemovalFrame(sentA[echoIndex++], RuneRankPlayerA);
            Assert.Equal(request, sentA[echoIndex]);
            AssertItemUsage(sentA[echoIndex], new MsgItemUsagePacket(
                RuneRankTargetUid, 102, MsgItemUsageAction.UnequipRune, 511_043_495, 0, 0, 0));
            AssertRuneUnequipManagedRefresh(sentA, echoIndex + 1, afterSessionA);
            Assert.Equal((ushort)212,
                Assert.Single(afterSessionA.PlayerItems.RuneItems, item => item.Uid == RuneRankTargetUid).Position);
            AssertRuneUntouchedSessionFields(originalSessionA, afterSessionA);
        }
        else
        {
            Assert.Same(originalSessionA, afterSessionA);
            Assert.Same(originalA, afterSessionA.PlayerItems);
            Assert.Same(originalSpellsA, afterSessionA.PlayerSpells);
            if (scenario == RuneUnequipSpellCase.ActiveVendor)
                AssertRuneUnequipVendorBoxDialog(sentA[beforeA]);
            else if (scenario == RuneUnequipSpellCase.FullInventory)
                AssertRuneUnequipMessageBox(sentA[beforeA], fullInventory: true);
            else if (scenario == RuneUnequipSpellCase.UnderLevel)
                Assert.Equal(LegacyMsgMessagePacket.SystemTalk(
                    "You need to be a second reborn hero to interact with rune system!").ToFrameBytes(), sentA[beforeA]);
        }

        if (removesSpell)
        {
            Assert.NotSame(originalSpellsA, afterSessionA.PlayerSpells);
            Assert.DoesNotContain(afterSessionA.PlayerSpells.Spells, entry => entry.Id == RuneUnequipSpellId);
        }
        else if (!succeeds)
            Assert.Same(originalSpellsA, afterSessionA.PlayerSpells);
        else
            Assert.Equal(initialSpellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(afterSessionA.PlayerSpells));
        Assert.Equal(unrelated, Assert.Single(afterSessionA.PlayerSpells.Spells, entry => entry.Id == unrelated.Id));
        Assert.Equal(expectedItemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(afterSessionA.PlayerItems));
        Assert.Equal(expectedSpellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(afterSessionA.PlayerSpells));
        Assert.Equal(expectedItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedSpellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(expectedItemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(expectedSpellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(
            new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Same(originalSessionB, GetSession(handler, connectionB));
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Same(originalSpellsB, GetSession(handler, connectionB).PlayerSpells);

        if (scenario == RuneUnequipSpellCase.AlreadyStoredRepeat)
        {
            var afterFirstSpells = afterSessionA.PlayerSpells;
            var repeatA = sentA.Count;
            await handler.HandleFrameAsync(connectionA, ReadFrame(request));
            Assert.Equal(repeatA + 5, sentA.Count);
            Assert.Equal(beforeB, sentB.Count);
            Assert.Equal(request, sentA[repeatA]);
            AssertRuneUnequipManagedRefresh(sentA, repeatA + 1, GetSession(handler, connectionA));
            Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(afterFirstSpells),
                PlayerSpellSnapshotLoader.ToLegacyBytes(GetSession(handler, connectionA).PlayerSpells));
            Assert.Equal(expectedItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
            Assert.Equal(expectedSpellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        }

        // B FIRST detects this action's rank publication before A's own query can repair the table.
        await AssertRuneRankingQueryAsync(handler, connectionB, sentB, RuneRankPlayerB,
            rank: succeeds ? 2 : 1, score: 10_000);
        if (succeeds)
            await AssertRuneRankingQueryAsync(handler, connectionA, sentA, RuneRankPlayerA, rank: 1, score: 10_600);
        Assert.Equal(originalItemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        Assert.Equal(initialSpellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(originalSpellsA));
        Assert.Equal(originalItemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(originalB));
        Assert.Equal(initialSpellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(originalSpellsB));
        Assert.Equal(originalItemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(initialSpellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        Assert.Equal(expectedItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedSpellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(initialSpellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(
            new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(RuneRankPlayerB)));
        Assert.Equal(originalItemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerB)));
        Assert.Contains(succeeds ? "committed MsgItemUsage unequip rune" : "skipped MsgItemUsage unequip rune",
            log.ToString());
    }

    private void SetRuneUnequipSpellFixtureClass(long uid)
    {
        var path = Path.Combine(_usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var source = File.ReadAllText(path);
        Assert.Equal(1, source.Split("Class=7051", StringSplitOptions.None).Length - 1);
        // Exact owned pre-entry fixture edit only: real4020517 admits Monk6000..6051.
        File.WriteAllText(path, source.Replace("Class=7051", "Class=6000", StringComparison.Ordinal));
    }

    private string RuneUnequipSpellFile(long uid) =>
        Path.Combine(_root, "PlayersSpells", uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private static void AssertRuneUnequipSpellRemovalFrame(byte[] frame, long uid)
    {
        Assert.Equal(new MsgActionPacket
        {
            ObjId = checked((uint)uid), DwParam = RuneUnequipSpellId, Type = (MsgActionType)109,
        }.ToFrameBytes(), frame);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(frame), out var removed));
        Assert.Equal(checked((uint)uid), removed.ObjId);
        Assert.Equal(RuneUnequipSpellId, removed.DwParam);
        Assert.Equal((MsgActionType)109, removed.Type);
        Assert.Equal(0U, removed.TargetUid);
        Assert.Equal(0, removed.DwParam2);
        Assert.Equal(0L, removed.DwParam3);
        Assert.False(removed.SucDone);
        Assert.Equal(0U, removed.TargetPositionX);
        Assert.Equal(0U, removed.TargetPositionY);
        Assert.Equal(0U, removed.Timestamp);
        Assert.Equal(0, removed.NpcId);
        Assert.Equal(0U, removed.CheatLogData1);
        Assert.Equal((ushort)0, removed.Facing);
        Assert.Equal(0U, removed.PositionX);
        Assert.Equal(0U, removed.PositionY);
        Assert.Equal(0U, removed.CheatLogData2);
        Assert.Equal(0U, removed.MapId);
        Assert.Equal(0U, removed.CheatLogData3);
        Assert.Equal(0, removed.ScheduleTime);
        Assert.Equal(0, removed.LinkIndex);
        Assert.Equal(0U, removed.CheatLogData4);
        Assert.Equal(0, removed.RemainTime);
        Assert.False(removed.MountCharge);
        Assert.Empty(removed.Strings);
        Assert.Empty(removed.UnknownFields);
    }
}
