using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneDropPreservesLoadedLearnedBookD11()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.RootD11_7899, RuneDropLearnedCase.Present);
    }

    [Fact]
    public async Task HandlerRuneDropPreservesLoadedLearnedBookD13()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.VisibleDx8D13, RuneDropLearnedCase.Present);
    }

    [Fact]
    public async Task HandlerRuneDropMissingUidPreservesLoadedLearnedBookD11()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.RootD11_7899, RuneDropLearnedCase.MissingUid);
    }

    [Fact]
    public async Task HandlerRuneDropMissingUidPreservesLoadedLearnedBookD13()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.VisibleDx8D13, RuneDropLearnedCase.MissingUid);
    }

    [Fact]
    public async Task HandlerRuneDropActiveVendorPreservesDifferentLevelLearnedBookD11()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.RootD11_7899, RuneDropLearnedCase.ActiveVendor);
    }

    [Fact]
    public async Task HandlerRuneDropActiveVendorPreservesDifferentLevelLearnedBookD13()
    {
        await AssertRuneDropLearnedBookAsync(ConquerProtocolProfile.VisibleDx8D13, RuneDropLearnedCase.ActiveVendor);
    }

    private enum RuneDropLearnedCase { Present, MissingUid, ActiveVendor }

    private async Task AssertRuneDropLearnedBookAsync(ConquerProtocolProfile profile, RuneDropLearnedCase scenario)
    {
        WriteSnapshot(RuneRankPlayerA, RuneRankPlayerA, mapId: 1_015, x: 826, y: 565);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerA);
        WriteItems(RuneRankPlayerA,
            PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneUnequipSpellItemId,
                position: 102, durability: 1, maximumDurability: 1, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId,
                position: 212, durability: 100, maximumDurability: 100, stackSize: 1));
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerB);

        // The same mapped ID is retained at metadata level16 and a different learned level15.
        var mapped = new PlayerSpellSnapshotEntry(RuneUnequipSpellId,
            (ushort)(scenario == RuneDropLearnedCase.ActiveVendor ? 15 : 16), 0, 0, 0, 0);
        var unrelated = new PlayerSpellSnapshotEntry(1_000, 2, 9_876, 3, 2, 1);
        var initialSpellsA = new PlayerSpellSnapshot([mapped, unrelated]);
        var initialSpellsB = new PlayerSpellSnapshot([mapped with { Experience = 789 }, unrelated]);
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        var spellBytesA = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(initialSpellsA.Spells);
        var spellBytesB = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(initialSpellsB.Spells);
        Assert.Equal(28, spellBytesA.Length); // Independent count2 + two12-byte records, ordinary zero padding.
        Assert.Equal((byte)0, spellBytesA[15]);
        Assert.Equal((byte)0, spellBytesA[27]);
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerA), spellBytesA);
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerB), spellBytesB);
        var itemBytesA = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA));
        var itemBytesB = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB));

        var catalog = CreateRuneBagFullInventoryFailureCatalog(
            [RuneRankSeed(RuneRankPlayerB, 10_000)], includeReturnedItem: true);
        var metadata = Assert.Single(catalog.ItemsById[(int)RuneUnequipSpellItemId]);
        Assert.Equal(6_000U, metadata.RequiredClass);
        Assert.Equal(1_416_016U, metadata.RuneSpellId);
        Assert.Equal(14_160U, metadata.RuneSpellId / 100);
        Assert.Equal(16U, metadata.RuneSpellId % 100);
        if (profile == ConquerProtocolProfile.VisibleDx8D13)
        {
            await WithOwnedRuneDropAsync(catalog, f => ExerciseAsync(f.Handler,
                f.Connection((uint)RuneRankPlayerA), f.Frames((uint)RuneRankPlayerA),
                f.Connection((uint)RuneRankPlayerB), f.Frames((uint)RuneRankPlayerB),
                f.Data.Users, f.Data.Items, f.Data.Spells, f.Log, nativeOwned: true),
                (uint)RuneRankPlayerA, (uint)RuneRankPlayerB);
        }
        else
        {
            // This remains the real managed-file D11 fixture. D11 has no joined
            // native item dependency owner; its old successful mutation is not
            // authority to fabricate that owner or broaden the D13 binding.
            using var log = new StringWriter();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
            Assert.Equal(spellsRoot, loader.PlayerSpellsRoot);
            var handler = new WorldServerFrameHandler(loader, handoff, catalog,
                LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: profile);
            var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
            var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
            await ExerciseAsync(handler, connectionA, sentA, connectionB, sentB,
                _usersRoot, _itemsRoot, spellsRoot, log, nativeOwned: false);
        }

        async Task ExerciseAsync(WorldServerFrameHandler handler,
            ConquerTcpConnection connectionA, List<byte[]> sentA, ConquerTcpConnection connectionB, List<byte[]> sentB,
            string userRoot, string itemRoot, string spellRoot, TextWriter log, bool nativeOwned)
        {
        var drops = nativeOwned && scenario != RuneDropLearnedCase.MissingUid;
        string RuneRankingItemsPath(long uid) => Path.Combine(itemRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        string RuneUnequipSpellFile(long uid) => Path.Combine(spellRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        string RuneDropLearnedCharacterFile(long uid) => Path.Combine(userRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        AssertRuneRankingStartup(sentA, rank: null, score: 10_600);
        AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);

        var entryA = ReadRuneDropLearnedSession(handler, connectionA);
        var entryB = ReadRuneDropLearnedSession(handler, connectionB);
        Assert.Equal(6_000L, entryA.Character.Class);
        Assert.Equal(2L, entryA.Character.Reborn);
        Assert.Equal(140L, entryA.Character.Level);
        Assert.Equal(1_015L, entryA.Character.MapId);
        Assert.Empty(entryA.PlayerItems.InventoryItems);
        Assert.Equal(2, entryA.PlayerItems.RuneItems.Count);
        var selected = Assert.Single(entryA.PlayerItems.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(RuneUnequipSpellItemId, selected.ItemId);
        Assert.Equal((ushort)102, selected.Position);
        var stored = Assert.Single(entryA.PlayerItems.RuneItems, item => item.Uid == RuneRankStoredUid);
        Assert.Equal(10_600U, LegacyRuneStoragePacket.ComputeStorageScore(entryA.PlayerItems));
        Assert.Equal(itemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(entryA.PlayerItems));
        Assert.Equal(itemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(entryB.PlayerItems));
        AssertRuneDropLearnedBook(entryA.PlayerSpells, mapped, unrelated, spellBytesA);
        Assert.Equal(spellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(entryB.PlayerSpells));
        Assert.Equal(itemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(itemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(spellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(spellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));

        if (scenario == RuneDropLearnedCase.ActiveVendor)
            await StartRuneDropLearnedVendorAsync(handler, connectionA, sentA, sentB);
        var beforeSessionA = ReadRuneDropLearnedSession(handler, connectionA);
        var beforeSessionB = ReadRuneDropLearnedSession(handler, connectionB);
        var nativeRowsBefore = nativeOwned
            ? Assert.IsType<LegacyNativeSpellStorage>(Assert.IsType<LegacyNativeGameClientRuntime>(connectionA.NativeGameClient).MySpells)
                .ClientSpells.ToDictionary(pair => pair.Key, pair => pair.Value)
            : null;
        Assert.Same(entryA.PlayerItems, beforeSessionA.PlayerItems);
        Assert.Same(entryA.PlayerSpells, beforeSessionA.PlayerSpells);
        Assert.Same(entryB, beforeSessionB);
        const uint missingUid = 5_349_999;
        Assert.DoesNotContain(beforeSessionA.PlayerItems.RuneItems, item => item.Uid == missingUid);
        var expectedItemsA = drops ? beforeSessionA.PlayerItems with
        {
            RuneItems = beforeSessionA.PlayerItems.RuneItems.Where(item => item.Uid != RuneRankTargetUid).ToArray(),
        } : beforeSessionA.PlayerItems;
        var expectedItemBytesA = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItemsA);
        var userBytesA = File.ReadAllBytes(RuneDropLearnedCharacterFile(RuneRankPlayerA));
        var userBytesB = File.ReadAllBytes(RuneDropLearnedCharacterFile(RuneRankPlayerB));
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        var root = Path.GetDirectoryName(userRoot)!;
        var lease = Path.Combine(itemRoot, ".managed-item-uids", "owner.lock");
        var beforeFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, lease))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        if (!nativeOwned)
        {
            Assert.Null(connectionA.NativeGameClient); Assert.Null(beforeSessionA.NativeGameClient);
            // Preserve the independent D11 wire/parser golden as serialization
            // evidence, without pretending it was emitted by a refused action.
            AssertRuneDropLearnedRemovalFrame(new MsgItemUsagePacket(
                RuneRankTargetUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        }
        // Ordinary complete87-byte body, declared0/empty tail; no needless offered UID or injected timestamp.
        await handler.HandleFrameAsync(connectionA, ReadFrame(new MsgItemUsagePacket(
            scenario == RuneDropLearnedCase.MissingUid ? missingUid : RuneRankTargetUid,
            0, MsgItemUsageAction.DropRune, 0, 0, 0, 0).ToFrameBytes()));

        Assert.Equal(beforeA + (drops ? 1 : 0), sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        if (drops)
        {
            AssertRuneDropLearnedRemovalFrame(sentA[beforeA]);
            Assert.False(MsgActionPacket.TryParse(ReadFrame(sentA[beforeA]), out _)); // No109 removal packet.
        }
        var afterA = ReadRuneDropLearnedSession(handler, connectionA);
        Assert.Equal(drops ? beforeSessionA with { Character = afterA.Character, PlayerItems = afterA.PlayerItems,
            PlayerSpells = afterA.PlayerSpells } : beforeSessionA, afterA);
        if (!drops)
        {
            Assert.Same(beforeSessionA, afterA);
            Assert.Same(beforeSessionA.Character, afterA.Character);
            Assert.Same(beforeSessionA.PlayerSpells, afterA.PlayerSpells);
        }
        var empty = new Dictionary<string, string>();
        Assert.Equal(beforeSessionA.Character with { Attributes = empty, PaidNobility = empty },
            afterA.Character with { Attributes = empty, PaidNobility = empty });
        Assert.Equal(beforeSessionA.Character.Attributes.OrderBy(pair => pair.Key), afterA.Character.Attributes.OrderBy(pair => pair.Key));
        Assert.Equal(beforeSessionA.Character.PaidNobility.OrderBy(pair => pair.Key), afterA.Character.PaidNobility.OrderBy(pair => pair.Key));
        Assert.Same(beforeSessionA.LegacyVendorListings, afterA.LegacyVendorListings);
        Assert.Equal(expectedItemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(afterA.PlayerItems));
        var retained = Assert.Single(afterA.PlayerItems.RuneItems, item => item.Uid == RuneRankStoredUid);
        if (!drops) Assert.Same(stored, retained);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], [], [stored])),
            PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], [], [retained])));
        Assert.Equal(drops, !afterA.PlayerItems.RuneItems.Any(item => item.Uid == RuneRankTargetUid));
        Assert.Equal(drops ? 5_000U : 10_600U, LegacyRuneStoragePacket.ComputeStorageScore(afterA.PlayerItems));
        AssertRuneDropLearnedBook(afterA.PlayerSpells, mapped, unrelated, spellBytesA);
        Assert.Equal(expectedItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(expectedItemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(itemRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(spellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        AssertRuneDropLearnedBook(new PlayerSpellSnapshotLoader(spellRoot).LoadByCharacterUid(RuneRankPlayerA),
            mapped, unrelated, spellBytesA);
        Assert.Same(beforeSessionB, ReadRuneDropLearnedSession(handler, connectionB));
        Assert.Equal(userBytesA, File.ReadAllBytes(RuneDropLearnedCharacterFile(RuneRankPlayerA)));
        Assert.Equal(userBytesB, File.ReadAllBytes(RuneDropLearnedCharacterFile(RuneRankPlayerB)));
        Assert.Equal(itemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(entryA.PlayerItems));
        Assert.Equal(spellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(entryA.PlayerSpells));

        var actualFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, lease)).Order().ToArray();
        Assert.Equal(beforeFiles.Keys.Order(), actualFiles);
        foreach (var pair in beforeFiles)
            Assert.Equal(drops && StringComparer.OrdinalIgnoreCase.Equals(pair.Key, RuneRankingItemsPath(RuneRankPlayerA))
                ? expectedItemBytesA : pair.Value, File.ReadAllBytes(pair.Key));
        if (nativeOwned)
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(connectionA.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(binding.Store.IsBoundSpellOwnerCurrent(binding));
            var rows = Assert.IsType<LegacyNativeSpellStorage>(connectionA.NativeGameClient.MySpells).ClientSpells;
            Assert.Equal(afterA.PlayerSpells.Spells.Select(row => row.Id).Order(), rows.Keys.Order());
            foreach (var row in afterA.PlayerSpells.Spells)
            {
                // PreparedSessionSpellMap deliberately reuses unchanged native rows.
                Assert.Same(nativeRowsBefore![row.Id], rows[row.Id]);
                Assert.Equal(row.Level, rows[row.Id].Level); Assert.Equal(row.Experience, rows[row.Id].Experience);
                Assert.Equal(row.PreviousLevel, rows[row.Id].PreviousLevel); Assert.Equal(row.SoulLevel, rows[row.Id].SoulLevel);
                Assert.Equal(row.UseSpellSoul, rows[row.Id].UseSpellSoul);
            }
        }
        else if (scenario != RuneDropLearnedCase.MissingUid)
            Assert.Contains("held MsgItemUsage drop rune reason=itemSpellPreparation:InvalidOperationException", log.ToString());
        // B FIRST: a missing UID refreshes eligible A; a successful D13 drop
        // removes A. A refused D11 mutation does neither. No A query repairs B.
        bool refreshed = nativeOwned || scenario == RuneDropLearnedCase.MissingUid;
        await AssertRuneRankingQueryAsync(handler, connectionB, sentB, RuneRankPlayerB,
            rank: drops || !refreshed ? 1 : 2, score: 10_000);
        await AssertRuneRankingQueryAsync(handler, connectionA, sentA, RuneRankPlayerA,
            rank: drops || !refreshed ? null : 1, score: drops ? 5_000U : 10_600U);
        Assert.Same(afterA.PlayerSpells, ReadRuneDropLearnedSession(handler, connectionA).PlayerSpells);
        Assert.Same(beforeSessionB.PlayerSpells, ReadRuneDropLearnedSession(handler, connectionB).PlayerSpells);
        Assert.Equal(itemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(entryA.PlayerItems));
        Assert.Equal(itemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(entryB.PlayerItems));
        Assert.Equal(spellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(entryA.PlayerSpells));
        Assert.Equal(spellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(entryB.PlayerSpells));
        Assert.Equal(expectedItemBytesA, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(itemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(spellBytesA, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(spellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        Assert.Equal(itemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(itemRoot).LoadByCharacterUid(RuneRankPlayerB)));
        Assert.Equal(spellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(
            new PlayerSpellSnapshotLoader(spellRoot).LoadByCharacterUid(RuneRankPlayerB)));
        Assert.Contains(drops ? "committed MsgItemUsage drop rune" : scenario == RuneDropLearnedCase.MissingUid
            ? "skipped MsgItemUsage drop rune" : "held MsgItemUsage drop rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage drop rune", log.ToString());
        Assert.False(LegacyTradeJournal.HasPending(userRoot));
        }
    }

    private string RuneDropLearnedCharacterFile(long uid) =>
        Path.Combine(_usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");

    private static WorldSession ReadRuneDropLearnedSession(
        WorldServerFrameHandler handler, ConquerTcpConnection connection) =>
        Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(connection));

    private static void AssertRuneDropLearnedBook(
        PlayerSpellSnapshot actual, PlayerSpellSnapshotEntry mapped,
        PlayerSpellSnapshotEntry unrelated, byte[] expectedBytes)
    {
        Assert.Equal(2, actual.Spells.Count);
        Assert.Equal(mapped, Assert.Single(actual.Spells, spell => spell.Id == RuneUnequipSpellId));
        Assert.Equal(unrelated, Assert.Single(actual.Spells, spell => spell.Id == unrelated.Id));
        Assert.Equal(expectedBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(actual));
    }

    private static async Task StartRuneDropLearnedVendorAsync(
        WorldServerFrameHandler handler, ConquerTcpConnection connection, List<byte[]> sentA, List<byte[]> sentB)
    {
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        var request = new MsgActionPacket
        {
            ObjId = checked((uint)RuneRankPlayerA), Type = MsgActionType.StartVendor,
        };
        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));
        Assert.Equal(beforeA + 2, sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sentA[beforeA]), out var booth));
        Assert.Equal(1_393_600U, booth.Uid);
        Assert.Equal(827, booth.X);
        Assert.Equal(565, booth.Y);
        Assert.Equal(406, booth.Mesh);
        Assert.Equal(14, booth.Type);
        Assert.Equal((request with { DwParam = booth.Uid, PositionX = 827, PositionY = 565 }).ToFrameBytes(),
            sentA[beforeA + 1]);
        var vending = ReadRuneDropLearnedSession(handler, connection);
        Assert.Equal("1393600", vending.Character.Attributes["RuntimeVendorUid"]);
        Assert.Equal("1015", vending.Character.Attributes["RuntimeVendorMapId"]);
        Assert.Equal("827", vending.Character.Attributes["RuntimeVendorX"]);
        Assert.Equal("565", vending.Character.Attributes["RuntimeVendorY"]);
        Assert.Same(LegacyVendorListingBook.Empty, vending.LegacyVendorListings);
        Assert.Empty(vending.LegacyVendorListings!.Listings);
    }

    private static void AssertRuneDropLearnedRemovalFrame(byte[] frame)
    {
        // Independent complete91-byte managed envelope; native timestamp0 fallback is separately unproved.
        byte[] expected =
        [
            0x5B, 0x00, 0x59, 0x08, 0x24, 0x9E, 0x51, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ];
        Assert.Equal(expected, frame);
        AssertItemUsage(frame, new MsgItemUsagePacket(
            RuneRankTargetUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
    }
}
