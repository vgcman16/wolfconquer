using System.Buffers.Binary;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(54)]
    public async Task NativeEquipmentRawLoadPublicEnterOwnsExactPrimaryAndAlternateSeparately(int position)
    {
        var fixture = await EnterRawEquipmentAsync(RawItems(
            RawItem(90, 620_003, checked((ushort)position)), RawItem(91, 410_003, 0)));
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        Assert.Same(fixture.Client, equipment.Owner);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        var selected = equipment.ObserveRawLoadedPosition(checked((ushort)position));
        Assert.True(selected.Available);
        Assert.True(selected.Found);
        var item = Assert.IsType<LegacyNativeEquipmentRawItem>(selected.Item);
        Assert.Equal(90U, item.Uid);
        Assert.Equal(620_003U, item.ItemId);
        Assert.Equal(checked((ushort)position), item.Position);
        Assert.Same(item, Assert.Single(selected.RawEntries).Value);
        Assert.Equal(90U, Assert.Single(selected.RawEntries).Key);
        var other = equipment.ObserveRawLoadedPosition(position == 4 ? (ushort)54 : (ushort)4);
        Assert.True(other.Available);
        Assert.False(other.Found);
        Assert.Null(other.Item);
        var beforePlayer = fixture.Client.Player;
        fixture.Client.Player = new LegacyNativePlayerRuntime(fixture.Client); // Declared MODEL Player replacement.
        Assert.NotSame(beforePlayer, fixture.Client.Player);
        Assert.Same(equipment, fixture.Client.RawEquipmentObservation); // Equipment is client-owned, not Player generation.
        Assert.Same(item, equipment.ObserveRawLoadedPosition(checked((ushort)position)).Item);
        GC.KeepAlive(fixture);
    }

    [Fact]
    public async Task NativeEquipmentRawLoadKeepsParentRoutingKeyAndItsDistinctDepositObject()
    {
        // Parent key/UID/Position remain 10/10/4 despite the child UID20/Position54.
        var fixture = await EnterRawEquipmentAsync(RawItems(
            RawItem(10, 620_003, 4, depositCount: 1), RawItem(20, 410_003, 54)),
            topLevelCount: 1);
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.True(observed.Available);
        Assert.True(observed.Found);
        var parent = Assert.IsType<LegacyNativeEquipmentRawItem>(observed.Item);
        Assert.Equal(10U, parent.Uid);
        Assert.Equal((ushort)4, parent.Position);
        Assert.Equal(620_003U, parent.ItemId);
        var entry = Assert.Single(observed.RawEntries);
        Assert.Equal(10U, entry.Key);
        Assert.Same(parent, entry.Value);
        var deposit = Assert.Single(parent.Deposits);
        Assert.Equal(20U, deposit.Key);
        Assert.Equal(20U, deposit.Value.Uid);
        Assert.Equal((ushort)54, deposit.Value.Position);
        Assert.NotSame(parent, deposit.Value);
        Assert.False(equipment.ObserveRawLoadedPosition(54).Found); // Child is a deposit, not a root equipment entry.
        GC.KeepAlive(fixture);
    }

    [Fact]
    public async Task NativeEquipmentRawLoadRejectsDuplicateSerializedIdentityAsUnavailable()
    {
        var fixture = await EnterRawEquipmentAsync(RawItems(
            RawItem(50, 410_003, 0), RawItem(50, 620_439, 4), RawItem(50, 620_003, 4)));
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.False(observed.Available); Assert.False(observed.Found); Assert.Null(observed.Item);
        Assert.Empty(observed.RawEntries); Assert.NotNull(observed.UnavailableReason);
        Assert.Empty(RawObservationItems(equipment)); // No first-TryAdd corrupted generation is published.
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        var session = Assert.IsType<WorldSession>(
            fixture.Handler.ReadCurrentSessionForNativeOwnerObservation(fixture.Connection));
        Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Empty(session.PlayerItems.EquipmentItems); // DTO behavior remains separately owned, not native authority.
        GC.KeepAlive(fixture);
    }

    [Theory]
    [InlineData(MsgItemUsageAction.Equip)]
    [InlineData(MsgItemUsageAction.Unequip)]
    public async Task NativeEquipmentRawLoadPublicMutationIntentInvalidatesBeforeInitialLog(MsgItemUsageAction action)
    {
        LegacyNativeEquipmentRuntime? observedOwner = null;
        var availableAtHandledLog = new List<bool>();
        using var log = new RawEquipmentLog(line =>
        {
            if (line?.StartsWith("WorldServer handled MsgItemUsage session=", StringComparison.Ordinal) == true)
                availableAtHandledLog.Add(observedOwner?.ObserveRawLoadedPosition(4).Available ?? true);
        });
        var fixture = await EnterRawEquipmentAsync(RawItems(RawItem(80, 620_003, 4)), log: log);
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        observedOwner = equipment;
        var originalItem = equipment.ObserveRawLoadedPosition(4).Item;
        Assert.NotNull(originalItem);
        // Equip's unknown UID is rejected; Unequip is slot-routed. Both are public mutation intents, not native writers.
        await fixture.Handler.HandleFrameAsync(fixture.Connection,
            ReadFrame(new MsgItemUsagePacket(999_999, 4, action, 0, 0, 0, 0).ToFrameBytes()));
        Assert.Equal(new[] { false }, availableAtHandledLog); // EXACT initial handled-log boundary.
        Assert.Same(equipment, fixture.Client.RawEquipmentObservation);
        var invalidated = equipment.ObserveRawLoadedPosition(4);
        Assert.False(invalidated.Available);
        Assert.False(invalidated.Found);
        Assert.Null(invalidated.Item); // Distinct from an Available=true genuinely empty lookup.
        Assert.NotNull(invalidated.UnavailableReason);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        GC.KeepAlive(fixture);
    }

    [Fact]
    public void NativeEquipmentRawLoadUsesOneActualLockedReadForBothConsumers()
    {
        var root = Path.Combine(_usersRoot, "RawItemsRead");
        Directory.CreateDirectory(root);
        var bytes = RawItems(RawItem(1, 620_003, 4));
        File.WriteAllBytes(Path.Combine(root, "1293602.bin"), bytes);
        var calls = 0;
        byte[]? captured = null;
        var snapshot = new PlayerItemSnapshotLoader(root).LoadByCharacterUid(1_293_602, raw =>
        {
            calls++;
            captured = raw;
            // Replace disk AFTER the single captured read; DTO MUST parse the exact captured original.
            File.WriteAllBytes(Path.Combine(root, "1293602.bin"), RawItems(RawItem(2, 410_003, 4)));
        });
        Assert.Equal(1, calls);
        Assert.Equal(bytes, captured);
        Assert.Equal(1U, Assert.Single(snapshot.EquipmentItems).Uid);
        Assert.Equal(620_003U, Assert.Single(snapshot.EquipmentItems).ItemId);
    }

    [Fact]
    public async Task NativeEquipmentRawLoadRuneConstructorDependencyIsUnavailableNotEmpty()
    {
        var fixture = await EnterRawEquipmentAsync(RawItems(
            RawItem(1, 620_003, 4), RawItem(2, 4_036_109, 103)));
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.False(observed.Available);
        Assert.False(observed.Found);
        Assert.Null(observed.Item);
        Assert.Contains("native rune item constructor", observed.UnavailableReason);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        GC.KeepAlive(fixture);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeEquipmentRawLoadConfiguredMissingOrEmptyFileHasActualEmptyOwner(bool missingFile)
    {
        var fixture = await EnterRawEquipmentAsync(missingFile ? null : RawItems());
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        Assert.Same(fixture.Client, equipment.Owner);
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.True(observed.Available);
        Assert.False(observed.Found);
        Assert.Null(observed.Item);
        Assert.Empty(observed.RawEntries);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        GC.KeepAlive(fixture);
    }

    [Fact]
    public async Task NativeEquipmentRawLoadAbsentConfiguredLoaderDoesNotFabricateEmptyOwner()
    {
        var fixture = await EnterRawEquipmentAsync(null, configureItemLoader: false);
        Assert.Null(fixture.Client.RawEquipmentObservation);
        var session = Assert.IsType<WorldSession>(
            fixture.Handler.ReadCurrentSessionForNativeOwnerObservation(fixture.Connection));
        Assert.Empty(session.PlayerItems.EquipmentItems);
        GC.KeepAlive(fixture);
    }

    [Fact]
    public async Task NativeEquipmentRawLoadTargetStarterReplacementInvalidatesRatherThanReserializesDto()
    {
        var fixture = await EnterRawEquipmentAsync(RawItems(), starterCandidate: true);
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        var session = Assert.IsType<WorldSession>(
            fixture.Handler.ReadCurrentSessionForNativeOwnerObservation(fixture.Connection));
        Assert.NotEmpty(session.PlayerItems.EquipmentItems); // Existing target starter DID replace empty DTO.
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.False(observed.Available);
        Assert.False(observed.Found);
        Assert.Null(observed.Item);
        Assert.Equal("targetStarterReplacement", observed.UnavailableReason);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        GC.KeepAlive(fixture);
    }

    [Theory]
    [InlineData(100, 59, true)]
    [InlineData(100, 60, false)]
    [InlineData(101, 4, false)]
    [InlineData(0, 63, true)]
    [InlineData(0, 64, false)]
    [InlineData(0, 213, false)]
    public async Task NativeEquipmentRawLoadPreservesWarehouseAndNormalPositionRouting(
        int warehouseId, int position, bool expectedMember)
    {
        var fixture = await EnterRawEquipmentAsync(RawItems(
            RawItem(77, 620_003, checked((ushort)position), checked((uint)warehouseId))));
        var equipment = Assert.IsType<LegacyNativeEquipmentRuntime>(fixture.Client.RawEquipmentObservation);
        var observed = equipment.ObserveRawLoadedPosition(checked((ushort)position));
        Assert.True(observed.Available);
        Assert.Equal(expectedMember, observed.Found);
        Assert.Equal(expectedMember ? 1 : 0, observed.RawEntries.Count);
        if (expectedMember)
        {
            var item = Assert.IsType<LegacyNativeEquipmentRawItem>(observed.Item);
            Assert.Equal(77U, item.Uid);
            Assert.Equal(620_003U, item.ItemId);
            Assert.Equal(checked((ushort)position), item.Position);
            Assert.Same(item, Assert.Single(observed.RawEntries).Value);
        }
        else Assert.Null(observed.Item);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
        GC.KeepAlive(fixture);
    }

    private async Task<RawEquipmentFixture> EnterRawEquipmentAsync(byte[]? bytes, int? topLevelCount = null,
        bool configureItemLoader = true, bool starterCandidate = false, TextWriter? log = null)
    {
        const uint uid = 1_293_602;
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(uid, uid: uid, reborn: starterCandidate ? 0 : 2,
            level: starterCandidate ? 1 : 140, classCode: starterCandidate ? 1005 : 7051,
            secondClass: starterCandidate ? 0 : 7050, money: starterCandidate ? 0 : 98_602_439_956,
            conquerPoints: starterCandidate ? 0 : 639_778_904, boundConquerPoints: starterCandidate ? 0 : 456);
        if (starterCandidate)
        {
            var path = Path.Combine(_usersRoot, uid + ".ini");
            File.WriteAllText(path, File.ReadAllText(path).Replace("BankCps=123", "BankCps=0", StringComparison.Ordinal));
        }
        var root = Path.Combine(_usersRoot, "ConfiguredRawItems");
        Directory.CreateDirectory(root);
        if (bytes is not null)
        {
            if (topLevelCount is { } count) BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
            File.WriteAllBytes(Path.Combine(root, uid + ".bin"), bytes);
        }
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(uid, "native-equipment-raw-load", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot,
            playerItemsRoot: configureItemLoader ? root : null, playerSpellsRoot: null,
            playerProficienciesRoot: null, playersCombatGearRoot: null, playerQuestsRoot: null), handoff, log);
        // Only physical client birth is declared MODEL. Equipment is NOT seeded here:
        // actual public Enter + the configured one-read loader must construct/adopt/hydrate it.
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.RawEquipmentObservation);
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, null,
            sendAsync: (memory, _) => { sent.Add(memory.ToArray()); return ValueTask.CompletedTask; },
            nativeGameClient: client);
        await handler.HandleFrameAsync(connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        var session = Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Same(client, session.NativeGameClient);
        Assert.Same(client.Player, session.NativePlayer);
        if (configureItemLoader) Assert.NotNull(client.RawEquipmentObservation);
        else Assert.Null(client.RawEquipmentObservation);
        return new(handler, connection, client, sent);
    }

    private static byte[] RawItem(uint uid, uint itemId, ushort position,
        uint warehouseId = 0, uint depositCount = 0)
    {
        var bytes = new byte[184];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), itemId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), position);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), warehouseId);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(136), depositCount);
        return bytes;
    }

    private static byte[] RawItems(params byte[][] rows)
    {
        var bytes = new byte[checked(8 + rows.Length * 184)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, rows.Length);
        for (var index = 0; index < rows.Length; index++) rows[index].CopyTo(bytes, 4 + index * 184);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), 0); // Perfection count.
        return bytes;
    }

    private sealed record RawEquipmentFixture(WorldServerFrameHandler Handler, ConquerTcpConnection Connection,
        LegacyNativeGameClientRuntime Client, List<byte[]> Sent);

    private sealed class RawEquipmentLog(Action<string?> onLine) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            onLine(value);
            base.WriteLine(value);
        }
    }
}
