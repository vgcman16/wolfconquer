using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemOperationAnimaTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint HostUid = 10_001;
    private const uint HostItemId = 410_339;
    private const uint SelectedAnimaUid = 20_002;
    private const uint FirstSameTypeAnimaUid = 20_001;
    private const uint AnimaItemId = 4_200_018;
    private const uint TimestampToken = 0x1234_5678;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-msg-item-operation-anima-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EmbedUsesActiveFieldsAndConsumesFirstInventoryItemOfSelectedType()
    {
        var host = Item(HostUid, HostItemId, position: 4);
        var firstSameType = Item(FirstSameTypeAnimaUid, AnimaItemId);
        var selected = Item(SelectedAnimaUid, AnimaItemId);
        var items = Items([firstSameType, selected], [host]);

        var result = await InvokeAsync(
            items,
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 77)));

        var storedHost = Assert.Single(result.FinalSession.PlayerItems.EquipmentItems);
        Assert.Equal(AnimaItemId, storedHost.AnimaItemId);
        var remaining = Assert.Single(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Equal(SelectedAnimaUid, remaining.Uid);
        AssertItemSnapshotsEqual(result.FinalSession.PlayerItems, result.PersistedItems);
        Assert.True(result.PersistedBeforeExposure);

        Assert.True(result.Sent.Count >= 3);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(result.Sent[0]), out var removal));
        Assert.Equal(FirstSameTypeAnimaUid, removal.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(result.Sent[1]), out var update));
        Assert.Equal(HostUid, update.Uid);
        Assert.Equal(AnimaItemId, update.AnimaItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
        Assert.Contains(
            result.Sent.Skip(2),
            bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains("WorldServer embedded Anima through active CMsgItemPB", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("WIRE-EVIDENCE", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveCreatesFreshUnboundAnimaThenClearsHost()
    {
        var host = Item(HostUid, HostItemId, position: 4, animaItemId: AnimaItemId);
        var result = await InvokeAsync(
            Items([], [host]),
            CreateRequest(HostUid, parameterItemUid: 0, action: 70),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 777)));

        var storedHost = Assert.Single(result.FinalSession.PlayerItems.EquipmentItems);
        Assert.Equal(0U, storedHost.AnimaItemId);
        var restored = Assert.Single(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Equal(HostUid + 1, restored.Uid);
        Assert.Equal(AnimaItemId, restored.ItemId);
        Assert.Equal((ushort)777, restored.Durability);
        Assert.Equal((ushort)777, restored.MaximumDurability);
        Assert.Equal((byte)0, restored.Bound);
        Assert.InRange(restored.Color, 3U, 8U);
        AssertItemSnapshotsEqual(result.FinalSession.PlayerItems, result.PersistedItems);
        Assert.True(result.PersistedBeforeExposure);

        Assert.True(result.Sent.Count >= 3);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(result.Sent[0]), out var add));
        Assert.Equal(restored.Uid, add.Uid);
        Assert.Equal(AnimaItemId, add.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, add.Mode);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(result.Sent[1]), out var update));
        Assert.Equal(HostUid, update.Uid);
        Assert.Equal(0U, update.AnimaItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
        Assert.Contains(
            result.Sent.Skip(2),
            bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains("WorldServer removed Anima through active CMsgItemPB", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbedAlsoUpdatesAnInventoryHost()
    {
        var host = Item(HostUid, HostItemId);
        var selected = Item(SelectedAnimaUid, AnimaItemId);
        var result = await InvokeAsync(
            Items([host, selected], []),
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create());

        var storedHost = Assert.Single(result.FinalSession.PlayerItems.InventoryItems);
        Assert.Equal(HostUid, storedHost.Uid);
        Assert.Equal(AnimaItemId, storedHost.AnimaItemId);
        Assert.Empty(result.FinalSession.PlayerItems.EquipmentItems);
        AssertItemSnapshotsEqual(result.FinalSession.PlayerItems, result.PersistedItems);
    }

    [Fact]
    public async Task RemoveAlsoUpdatesAnInventoryHost()
    {
        var host = Item(HostUid, HostItemId, animaItemId: AnimaItemId);
        var result = await InvokeAsync(
            Items([host], []),
            CreateRequest(HostUid, parameterItemUid: 0, action: 70),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 77)));

        Assert.Equal(2, result.FinalSession.PlayerItems.InventoryItems.Count);
        var storedHost = Assert.Single(
            result.FinalSession.PlayerItems.InventoryItems,
            item => item.Uid == HostUid);
        Assert.Equal(0U, storedHost.AnimaItemId);
        var restored = Assert.Single(
            result.FinalSession.PlayerItems.InventoryItems,
            item => item.Uid != HostUid);
        Assert.Equal(AnimaItemId, restored.ItemId);
        AssertItemSnapshotsEqual(result.FinalSession.PlayerItems, result.PersistedItems);
    }

    [Fact]
    public async Task InactiveAnimaPinBlocksEmbedBeforeAnyMutation()
    {
        var initial = Items(
            [Item(SelectedAnimaUid, AnimaItemId)],
            [Item(HostUid, HostItemId, position: 4)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create(),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PinCodeAnima"] = "abc123",
            });

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        var reply = Assert.Single(result.Sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(reply), out var denial));
        Assert.Equal("Please Active Pincode", denial.Text);
        Assert.Contains("source=MsgItemUsuagePacket.EmbedAnima", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisallowedHostPositionIsSafetyFencedWithoutConsumingAnima()
    {
        var initial = Items(
            [Item(SelectedAnimaUid, AnimaItemId)],
            [Item(HostUid, HostItemId, position: 9)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create());

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=host-position-disallowed", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullInventoryBlocksRemoveWithoutClearingHost()
    {
        var inventory = Enumerable.Range(1, 40)
            .Select(index => Item((uint)index, (uint)(700_000 + index)))
            .ToArray();
        var initial = Items(
            inventory,
            [Item(HostUid, HostItemId, position: 4, animaItemId: AnimaItemId)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, parameterItemUid: 0, action: 70),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 77)));

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=inventory-full", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveRequiresActiveClientZeroParameterField()
    {
        var initial = Items(
            [],
            [Item(HostUid, HostItemId, position: 4, animaItemId: AnimaItemId)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, parameterItemUid: 99, action: 70),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 77)));

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=remove-parameter-not-zero", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingItemPersistenceRootPreventsSessionOrPacketExposure()
    {
        var initial = Items(
            [Item(SelectedAnimaUid, AnimaItemId)],
            [Item(HostUid, HostItemId, position: 4)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create(),
            provideItemsRoot: false);

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=player-items-root-missing", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedItemPersistencePreventsSessionOrPacketExposure()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-items-root-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockedRoot, "not-a-directory");
        var initial = Items(
            [Item(SelectedAnimaUid, AnimaItemId)],
            [Item(HostUid, HostItemId, position: 4)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, SelectedAnimaUid, action: 69),
            LegacyItemWorldDataFixture.Create(),
            playerItemsRootOverride: blockedRoot);

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=item-persistence-failed:", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveWithoutCatalogMetadataDoesNotLoseEmbeddedAnima()
    {
        var initial = Items(
            [],
            [Item(HostUid, HostItemId, position: 4, animaItemId: AnimaItemId)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, parameterItemUid: 0, action: 70),
            LegacyItemWorldDataFixture.Create());

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=anima-itemtype-missing-or-invalid", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItemUidExhaustionDoesNotClearEmbeddedAnima()
    {
        var initial = Items(
            [Item(uint.MaxValue, 700_001)],
            [Item(HostUid, HostItemId, position: 4, animaItemId: AnimaItemId)]);
        var result = await InvokeAsync(
            initial,
            CreateRequest(HostUid, parameterItemUid: 0, action: 70),
            LegacyItemWorldDataFixture.Create(ItemType(AnimaItemId, durability: 77)));

        Assert.Same(result.InitialSession, result.FinalSession);
        AssertItemSnapshotsEqual(initial, result.PersistedItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=item-uid-exhausted", result.Log, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot initialItems,
        byte[] request,
        WorldDataCatalog worldData,
        IReadOnlyDictionary<string, string>? attributes = null,
        bool provideItemsRoot = true,
        string? playerItemsRootOverride = null)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var itemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        Directory.CreateDirectory(itemsRoot);
        var loader = new PlayerItemSnapshotLoader(itemsRoot);
        loader.SaveByCharacterUid(AccountId, initialItems);

        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9_958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var initialSession = Session(initialItems, attributes ?? new Dictionary<string, string>());
        var persistenceArmed = false;
        var persistedBeforeExposure = false;
        var sessions = new WorldSessionStore((changedConnection, previous, current) =>
        {
            if (persistenceArmed && ReferenceEquals(changedConnection, connection) && previous is not null)
            {
                persistedBeforeExposure = PlayerItemSnapshotLoader
                    .ToLegacyBytes(loader.LoadByCharacterUid(AccountId))
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            }
        });
        sessions.Set(connection, initialSession);
        persistenceArmed = true;

        await Handlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: initialSession,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: provideItemsRoot ? playerItemsRootOverride ?? itemsRoot : null),
            ReadFrame(request));

        Assert.True(sessions.TryGet(connection, out var finalSession));
        return new InvokeResult(
            initialSession,
            Assert.IsType<WorldSession>(finalSession),
            loader.LoadByCharacterUid(AccountId),
            persistedBeforeExposure,
            sent,
            log.ToString());
    }

    private static WorldSession Session(
        PlayerItemSnapshot items,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId,
            Username: "msg-item-operation-anima-test",
            Character: new CharacterSnapshot(
                Uid: AccountId,
                Name: "AnimaTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 1_000,
                ConquerPoints: 50,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: "fixture");
    }

    private static PlayerItemSnapshot Items(
        IReadOnlyList<PlayerItemSnapshotItem> inventory,
        IReadOnlyList<PlayerItemSnapshotItem> equipment)
    {
        return new PlayerItemSnapshot(inventory, [], [], equipment, []);
    }

    private static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId,
        ushort position = 0,
        uint animaItemId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 10,
            MaximumDurability = 10,
            Position = position,
            StackSize = 1,
            Color = 5,
            AnimaItemId = animaItemId,
        };
    }

    private static ItemType ItemType(uint itemId, uint durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "AnimaTestItem";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), fields[1], fields);
    }

    private static byte[] CreateRequest(uint itemUid, uint parameterItemUid, uint action)
    {
        var payload = new List<byte>();
        WriteVarintField(payload, fieldNumber: 1, itemUid);
        WriteVarintField(payload, fieldNumber: 3, parameterItemUid);
        WriteVarintField(payload, fieldNumber: 6, action);
        WriteVarintField(payload, fieldNumber: 7, TimestampToken);
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemOperation, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, checked((uint)(fieldNumber << 3)));
        WriteVarint(payload, value);
    }

    private static void WriteVarint(List<byte> payload, uint value)
    {
        do
        {
            var current = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                current |= 0x80;
            }

            payload.Add(current);
        }
        while (value != 0);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static void AssertItemSnapshotsEqual(
        PlayerItemSnapshot expected,
        PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession FinalSession,
        PlayerItemSnapshot PersistedItems,
        bool PersistedBeforeExposure,
        IReadOnlyList<byte[]> Sent,
        string Log);
}
