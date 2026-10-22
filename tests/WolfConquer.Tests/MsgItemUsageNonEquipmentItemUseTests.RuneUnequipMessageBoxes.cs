using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerRuneUnequipFullInventoryUsesMessageBoxD11()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.RootD11_7899,
            fullInventory: true, vendor: false, missingUid: false, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipFullInventoryUsesMessageBoxD13()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.VisibleDx8D13,
            fullInventory: true, vendor: false, missingUid: false, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipVendorUsesMessageBoxD11()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.RootD11_7899,
            fullInventory: false, vendor: true, missingUid: false, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipVendorUsesMessageBoxD13()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.VisibleDx8D13,
            fullInventory: false, vendor: true, missingUid: false, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipVendorMissingUidUsesMessageBoxD11()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.RootD11_7899,
            fullInventory: false, vendor: true, missingUid: true, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipVendorMissingUidUsesMessageBoxD13()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.VisibleDx8D13,
            fullInventory: false, vendor: true, missingUid: true, pending: false);
    }

    [Fact]
    public async Task HandlerRuneUnequipPendingMovementPromptSurvivesFullInventoryD11()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.RootD11_7899,
            fullInventory: true, vendor: false, missingUid: false, pending: true);
    }

    [Fact]
    public async Task HandlerRuneUnequipPendingMovementPromptSurvivesFullInventoryD13()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.VisibleDx8D13,
            fullInventory: true, vendor: false, missingUid: false, pending: true);
    }

    [Fact]
    public async Task HandlerRuneUnequipPendingMovementPromptSurvivesVendorD11()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.RootD11_7899,
            fullInventory: false, vendor: true, missingUid: false, pending: true);
    }

    [Fact]
    public async Task HandlerRuneUnequipPendingMovementPromptSurvivesVendorD13()
    {
        await AssertRuneUnequipMessageBoxScenarioAsync(ConquerProtocolProfile.VisibleDx8D13,
            fullInventory: false, vendor: true, missingUid: false, pending: true);
    }

    private async Task AssertRuneUnequipMessageBoxScenarioAsync(
        ConquerProtocolProfile profile, bool fullInventory, bool vendor, bool missingUid, bool pending)
    {
        Assert.True(fullInventory ^ vendor);
        Assert.False(missingUid && !vendor);
        WriteSnapshot(RuneRankPlayerA, RuneRankPlayerA, mapId: pending ? 10_428 : 1_015,
            x: pending ? 60 : 826, y: pending ? 67 : 565);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerA);
        var physical = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        if (fullInventory)
            for (var index = 0; index < 40; index++)
                physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
                    6_420_000U + (uint)index, 3_306_366, position: 0,
                    durability: 1, maximumDurability: 1, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid, RuneUnequipSpellItemId, position: 102,
            durability: 1, maximumDurability: 1, stackSize: 1));
        physical.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, physical.ToArray());
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        SetRuneUnequipSpellFixtureClass(RuneRankPlayerB);

        var mapped = new PlayerSpellSnapshotEntry(RuneUnequipSpellId, 16, 0, 0, 0, 0);
        var unrelated = new PlayerSpellSnapshotEntry(1_000, 2, 9_876, 3, 2, 1);
        var spellsA = new PlayerSpellSnapshot([mapped, unrelated]);
        var spellsB = new PlayerSpellSnapshot([mapped with { Experience = 789 }, unrelated]);
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        var spellBytesA = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(spellsA.Spells);
        var spellBytesB = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(spellsB.Spells);
        // Owned pre-entry files only: no post-entry session injection or manual Save.
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerA), spellBytesA);
        File.WriteAllBytes(RuneUnequipSpellFile(RuneRankPlayerB), spellBytesB);
        var itemBytesA = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA));
        var itemBytesB = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB));
        var catalog = CreateRuneBagFullInventoryFailureCatalog(
            [RuneRankSeed(RuneRankPlayerB, 10_000)], includeReturnedItem: true);
        var metadata = Assert.Single(catalog.ItemsById[(int)RuneUnequipSpellItemId]);
        Assert.Equal(6_000U, metadata.RequiredClass);
        Assert.Equal(1_416_016U, metadata.RuneSpellId);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog,
            LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: profile);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        // Packet-filter helpers, not profile-invariant total startup counts.
        AssertRuneRankingStartup(sentA, rank: null, score: 10_600);
        AssertRuneRankingStartup(sentB, rank: 1, score: 10_000);
        var entryA = GetSession(handler, connectionA);
        var entryB = GetSession(handler, connectionB);
        AssertRuneUnequipMessageBoxPhysical(entryA, itemBytesA, spellBytesA, spellsA,
            fullInventory ? 40 : 0, pending ? 10_428 : 1_015, pending ? 60 : 826, pending ? 67 : 565);
        Assert.Equal(itemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(entryB.PlayerItems));
        Assert.Equal(spellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(entryB.PlayerSpells));
        Assert.Equal(itemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(spellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        Assert.False(entryA.Character.Attributes.ContainsKey("PendingMessageBox"));
        Assert.False(entryA.Character.Attributes.ContainsKey("RuntimeVendorUid"));

        if (pending)
        {
            var beforeMoveA = sentA.Count;
            var beforeMoveB = sentB.Count;
            var movement = new MsgMovementPacket
            {
                Direction = 5, Uid = 0, Running = MsgMovementMode.Walk, Timestamp = 456,
            };
            await handler.HandleFrameAsync(connectionA, ReadFrame(movement.ToFrameBytes()));
            Assert.Equal(beforeMoveA + 2, sentA.Count);
            Assert.Equal(beforeMoveB, sentB.Count);
            Assert.Equal((movement with
            {
                Uid = checked((uint)RuneRankPlayerA), MapId = 10_428,
            }).ToFrameBytes(), sentA[beforeMoveA]);
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sentA[beforeMoveA + 1]), out var prompt));
            Assert.Equal(0U, prompt.Unknown);
            Assert.Equal(ushort.MaxValue, prompt.InputMaxLength);
            Assert.Equal((byte)0, prompt.OptionId);
            Assert.Equal(LegacyNpcReplyInteractType.MessageBox, prompt.InteractType);
            Assert.Equal("Are you sure you want to leave the alien world and return to Twin City?", prompt.Text);
            var moved = GetSession(handler, connectionA);
            Assert.Equal("AlienWorldLeave", moved.Character.Attributes["PendingMessageBox"]);
            Assert.Same(entryA.PlayerItems, moved.PlayerItems);
            Assert.Same(entryA.PlayerSpells, moved.PlayerSpells);
            Assert.Equal(60L, entryA.Character.X);
            Assert.Equal(67L, entryA.Character.Y);
            Assert.False(entryA.Character.Attributes.ContainsKey("PendingMessageBox"));
            AssertRuneUnequipMessageBoxPhysical(moved, itemBytesA, spellBytesA, spellsA,
                fullInventory ? 40 : 0, 10_428, 61, 66);
        }

        if (vendor)
        {
            var prior = GetSession(handler, connectionA);
            var beforeVendorA = sentA.Count;
            var beforeVendorB = sentB.Count;
            var startRequest = new MsgActionPacket
            {
                ObjId = checked((uint)RuneRankPlayerA), Type = MsgActionType.StartVendor,
            };
            await handler.HandleFrameAsync(connectionA, ReadFrame(startRequest.ToFrameBytes()));
            Assert.Equal(beforeVendorA + 2, sentA.Count);
            Assert.Equal(beforeVendorB, sentB.Count);
            Assert.True(LegacySobNpcSpawnPacket.TryParse(ReadFrame(sentA[beforeVendorA]), out var booth));
            Assert.Equal(1_393_600U, booth.Uid);
            Assert.Equal(pending ? 62 : 827, booth.X);
            Assert.Equal(pending ? 66 : 565, booth.Y);
            Assert.Equal(406, booth.Mesh);
            Assert.Equal(14, booth.Type);
            Assert.Equal((startRequest with
            {
                DwParam = booth.Uid,
                PositionX = checked((uint)booth.X), PositionY = checked((uint)booth.Y),
            }).ToFrameBytes(), sentA[beforeVendorA + 1]);
            var vending = GetSession(handler, connectionA);
            Assert.Equal("1393600", vending.Character.Attributes["RuntimeVendorUid"]);
            Assert.Equal(pending ? "10428" : "1015", vending.Character.Attributes["RuntimeVendorMapId"]);
            Assert.Equal(pending ? "62" : "827", vending.Character.Attributes["RuntimeVendorX"]);
            Assert.Equal(pending ? "66" : "565", vending.Character.Attributes["RuntimeVendorY"]);
            Assert.Same(LegacyVendorListingBook.Empty, vending.LegacyVendorListings);
            Assert.Empty(vending.LegacyVendorListings!.Listings);
            foreach (var attribute in prior.Character.Attributes)
                Assert.Equal(attribute.Value, vending.Character.Attributes[attribute.Key]);
            Assert.Same(prior.PlayerItems, vending.PlayerItems);
            Assert.Same(prior.PlayerSpells, vending.PlayerSpells);
            AssertRuneUnequipMessageBoxPhysical(vending, itemBytesA, spellBytesA, spellsA,
                0, pending ? 10_428 : 1_015, pending ? 61 : 826, pending ? 66 : 565);
        }

        var beforeSession = GetSession(handler, connectionA);
        var pendingAttributes = beforeSession.Character.Attributes
            .Where(pair => pair.Key.StartsWith("PendingMessageBox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        Assert.Equal(pending ? 1 : 0, pendingAttributes.Length); // This producer owns ONE discriminator only.
        if (pending)
            Assert.Equal("AlienWorldLeave", Assert.Single(pendingAttributes).Value);
        const uint absentUid = 5_349_999;
        Assert.DoesNotContain(beforeSession.PlayerItems.RuneItems, item => item.Uid == absentUid);
        var request = CreateRuneEquipmentUse(
            missingUid ? absentUid : RuneRankTargetUid, dwParam: 102, MsgItemUsageAction.UnequipRune);
        var beforeA = sentA.Count;
        var beforeB = sentB.Count;
        await handler.HandleFrameAsync(connectionA, ReadFrame(request));

        // First source-predicted RED boundary is the complete2255 literal, not the packet count.
        Assert.Equal(beforeA + 1, sentA.Count);
        Assert.Equal(beforeB, sentB.Count);
        AssertRuneUnequipMessageBox(sentA[beforeA], fullInventory);
        var after = GetSession(handler, connectionA);
        Assert.Same(beforeSession, after);
        Assert.Same(entryA.PlayerItems, after.PlayerItems);
        Assert.Same(entryA.PlayerSpells, after.PlayerSpells);
        Assert.Same(beforeSession.LegacyVendorListings, after.LegacyVendorListings);
        Assert.Equal(pendingAttributes, after.Character.Attributes
            .Where(pair => pair.Key.StartsWith("PendingMessageBox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
        AssertRuneUnequipMessageBoxPhysical(after, itemBytesA, spellBytesA, spellsA,
            fullInventory ? 40 : 0, pending ? 10_428 : 1_015,
            pending ? 61 : 826, pending ? 66 : 565);
        Assert.Equal(itemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(entryA.PlayerItems));
        Assert.Equal(spellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(entryA.PlayerSpells));
        Assert.Same(entryB, GetSession(handler, connectionB));
        Assert.Equal(itemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(spellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        // Specific frozen neutral-query contract; not a startup-count or generic profile assumption.
        await AssertRuneRankingQueryAsync(handler, connectionB, sentB, RuneRankPlayerB, rank: 1, score: 10_000);
        Assert.Equal(itemBytesA, PlayerItemSnapshotLoader.ToLegacyBytes(entryA.PlayerItems));
        Assert.Equal(spellBytesA, PlayerSpellSnapshotLoader.ToLegacyBytes(entryA.PlayerSpells));
        Assert.Equal(itemBytesB, PlayerItemSnapshotLoader.ToLegacyBytes(entryB.PlayerItems));
        Assert.Equal(spellBytesB, PlayerSpellSnapshotLoader.ToLegacyBytes(entryB.PlayerSpells));
        Assert.Equal(itemBytesB, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Equal(spellBytesB, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerB)));
        var output = log.ToString();
        Assert.Contains(fullInventory ? "inventory is full." : "source rejects active vending.", output);
        Assert.DoesNotContain("rune item was not found.", output);
        Assert.DoesNotContain("handled MsgItemUsage unequip rune", output);
        Assert.DoesNotContain("persisted MsgItemUsage unequip rune", output);
    }

    private void AssertRuneUnequipMessageBoxPhysical(
        WorldSession session, byte[] itemBytes, byte[] spellBytes, PlayerSpellSnapshot spells,
        int inventoryCount, long map, long x, long y)
    {
        Assert.Equal(6_000L, session.Character.Class);
        Assert.Equal(2L, session.Character.Reborn);
        Assert.Equal(140L, session.Character.Level);
        Assert.Equal(map, session.Character.MapId);
        Assert.Equal(x, session.Character.X);
        Assert.Equal(y, session.Character.Y);
        Assert.Equal(inventoryCount, session.PlayerItems.InventoryItems.Count);
        Assert.Equal(2, session.PlayerItems.RuneItems.Count);
        var rune = Assert.Single(session.PlayerItems.RuneItems, item => item.Uid == RuneRankTargetUid);
        Assert.Equal(RuneUnequipSpellItemId, rune.ItemId);
        Assert.Equal((ushort)102, rune.Position);
        Assert.Equal((ushort)1, rune.Durability);
        Assert.Equal((ushort)1, rune.MaximumDurability);
        Assert.Equal(10_600U, LegacyRuneStoragePacket.ComputeStorageScore(session.PlayerItems));
        Assert.Equal(spells.Spells, session.PlayerSpells.Spells);
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems));
        Assert.Equal(spellBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(session.PlayerSpells));
        Assert.Equal(itemBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        Assert.Equal(spellBytes, File.ReadAllBytes(RuneUnequipSpellFile(RuneRankPlayerA)));
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA)));
        Assert.Equal(spellBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(
            new PlayerSpellSnapshotLoader(Path.Combine(_root, "PlayersSpells")).LoadByCharacterUid(RuneRankPlayerA)));
    }

    private static void AssertRuneUnequipMessageBox(byte[] frame, bool fullInventory)
    {
        // Independent literal full envelopes; no factory or packet writer builds expected bytes.
        byte[] expected = fullInventory
            ? [
            0x3A, 0x00, 0xCF, 0x08, 0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0x06, 0x01, 0x2B, 0x00, 0x50,
            0x6C, 0x65, 0x61, 0x73, 0x65, 0x20, 0x6D, 0x61,
            0x6B, 0x65, 0x20, 0x31, 0x20, 0x6D, 0x6F, 0x72,
            0x65, 0x20, 0x73, 0x70, 0x61, 0x63, 0x65, 0x20,
            0x69, 0x6E, 0x20, 0x79, 0x6F, 0x75, 0x72, 0x20,
            0x69, 0x6E, 0x76, 0x65, 0x6E, 0x74, 0x6F, 0x72,
            0x79, 0x2E,
            ]
            : [
            0x37, 0x00, 0xCF, 0x08, 0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0x06, 0x01, 0x28, 0x00, 0x53,
            0x6F, 0x72, 0x72, 0x79, 0x2C, 0x20, 0x79, 0x6F,
            0x75, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F, 0x74,
            0x20, 0x64, 0x6F, 0x20, 0x74, 0x68, 0x69, 0x73,
            0x20, 0x77, 0x68, 0x69, 0x6C, 0x65, 0x20, 0x76,
            0x65, 0x6E, 0x64, 0x69, 0x6E, 0x67, 0x2E,
            ];
        var text = fullInventory ? "Please make 1 more space in your inventory." : RuneUnequipVendorDialogText;
        var textLength = (ushort)(fullInventory ? 43 : 40);
        Assert.Equal(expected, frame);
        Assert.Equal(fullInventory ? 58 : 55, frame.Length);
        Assert.Equal((ushort)2255, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2)));
        Assert.True(PacketHeader.TryRead(frame, out var header));
        Assert.Equal((ushort)frame.Length, header.Length);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var message));
        Assert.Equal(0U, message.Unknown);
        Assert.Equal(ushort.MaxValue, message.InputMaxLength);
        Assert.Equal((byte)0, message.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, message.InteractType);
        Assert.Equal(text, message.Text);
        Assert.Equal((byte)1, frame[12]);
        Assert.Equal(textLength, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(13, 2)));
        Assert.Equal(text, Encoding.UTF8.GetString(frame.AsSpan(15)));
        var sequence = new ReadOnlySequence<byte>(frame);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out _, out var consumed, out _));
        Assert.Equal(sequence.End, consumed);
        byte[] expectedWire = [.. expected, 0x54, 0x51, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72];
        var wire = LegacyGameServerFrameCodec.AppendSeal(frame);
        Assert.Equal(expectedWire, wire);
        Assert.Equal(fullInventory ? 66 : 63, wire.Length);
        Assert.True(LegacyGameServerFrameCodec.TryStripSeal(wire, out var restored, out var wireConsumed));
        Assert.Equal(wire.Length, wireConsumed);
        Assert.Equal(expected, restored); // One seal, full consumption, no trailing Finish/second packet.
    }
}
