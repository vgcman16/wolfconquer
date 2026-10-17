using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemUsageWireEvidenceTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ActiveNpcId = 489_123;
    private const uint EquipmentUid = 0x01020304;
    private const uint EquipmentItemId = 410_339;
    private const uint ReferencedItemUid = 0x05060708;
    private const uint ReferencedItemId = 4_200_018;
    private const uint Timestamp = 0x11121314;
    private const uint DwParam2 = 0x21222324;
    private const uint DwParam3 = 0x31323334;
    private const uint DwParam4 = 0x41424344;
    private const int PayloadHexLimit = 256;
    private const string WireEvidencePrefix = "WIRE-EVIDENCE MsgItemUsageFixedLayout";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-item-usage-wire-evidence-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;
    private readonly string _characterSnapshotPath;
    private readonly string _itemSnapshotPath;

    public LegacyItemUsageWireEvidenceTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        _characterSnapshotPath = Path.Combine(_usersRoot, AccountId + ".ini");
        _itemSnapshotPath = Path.Combine(_itemsRoot, CharacterUid + ".bin");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        File.WriteAllBytes(
            _characterSnapshotPath,
            Encoding.ASCII.GetBytes("[Character]\r\nUID=1293600\r\nMoney=123456\r\n"));
        File.WriteAllBytes(_itemSnapshotPath, [0x57, 0x49, 0x52, 0x45]);
    }

    [Theory]
    [InlineData((ushort)0xBEEF, true)]
    [InlineData((ushort)0xCAFE, false)]
    public async Task UnsupportedFixedLayoutActionsLogFullPayloadAndPreserveState(
        ushort actionId,
        bool includesReferencedItemUid)
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(CreateItemUsageEvidencePlayerItems());
        sessions.Set(connection, session);
        var dwParam = includesReferencedItemUid ? ReferencedItemUid : 0UL;
        var request = CreateItemUsageFrame(
            actionId,
            EquipmentUid,
            dwParam,
            Timestamp,
            DwParam2,
            DwParam3,
            DwParam4,
            MsgItemUsagePacket.PayloadLength);
        var payload = request.AsSpan(PacketHeader.Size).ToArray();
        var characterSnapshotBefore = File.ReadAllBytes(_characterSnapshotPath);
        var itemSnapshotBefore = File.ReadAllBytes(_itemSnapshotPath);

        Assert.Equal(MsgItemUsagePacket.PayloadLength, payload.Length);
        Assert.All(
            payload[MsgItemUsagePacket.MinimumPayloadLength..],
            value => Assert.NotEqual(0, value));

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(request));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var sessionAfter));
        Assert.Same(session, sessionAfter);
        Assert.Same(session.Character, sessionAfter!.Character);
        Assert.Same(session.PlayerItems, sessionAfter.PlayerItems);
        Assert.Equal(characterSnapshotBefore, File.ReadAllBytes(_characterSnapshotPath));
        Assert.Equal(itemSnapshotBefore, File.ReadAllBytes(_itemSnapshotPath));

        var output = log.ToString();
        var evidence = Assert.Single(WireEvidenceLines(output));
        Assert.StartsWith(WireEvidencePrefix, evidence, StringComparison.Ordinal);
        Assert.Contains($"opcode={WorldPacketOpcodes.MsgItemUsage}", evidence, StringComparison.Ordinal);
        Assert.Contains($"actionId={actionId}", evidence, StringComparison.Ordinal);
        Assert.Contains($"id={EquipmentUid}", evidence, StringComparison.Ordinal);
        Assert.Contains($"dwParam={dwParam}", evidence, StringComparison.Ordinal);
        Assert.Contains($"timestamp={Timestamp}", evidence, StringComparison.Ordinal);
        Assert.Contains($"dwParam2={DwParam2}", evidence, StringComparison.Ordinal);
        Assert.Contains($"dwParam3={DwParam3}", evidence, StringComparison.Ordinal);
        Assert.Contains($"dwParam4={DwParam4}", evidence, StringComparison.Ordinal);
        Assert.Contains($"activeNpcId={ActiveNpcId}", evidence, StringComparison.Ordinal);
        Assert.Contains(
            $"payloadLength={MsgItemUsagePacket.PayloadLength} payloadHexBytes={MsgItemUsagePacket.PayloadLength} payloadHexTruncated=false",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains($"payloadHex={Convert.ToHexString(payload)}", evidence, StringComparison.Ordinal);
        Assert.Contains($"fullPayloadSha256={Sha256(payload)}", evidence, StringComparison.Ordinal);
        Assert.Contains(
            $"id={EquipmentUid}->equipment(uid={EquipmentUid},itemId={EquipmentItemId},animaItemId={ReferencedItemId},position=4,warehouseId=0)",
            evidence,
            StringComparison.Ordinal);

        if (includesReferencedItemUid)
        {
            Assert.Contains(
                $"dwParamLow32={ReferencedItemUid}->inventory(uid={ReferencedItemUid},itemId={ReferencedItemId},animaItemId=0,position=0,warehouseId=0)",
                evidence,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("dwParamLow32=", evidence, StringComparison.Ordinal);
        }

        Assert.Contains("PARITY-GAP category=item severity=warning gap=item-use", output, StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP unsupported MsgItemUsage", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedActionUidResolutionSearchesEverySessionItemCollection()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var playerItems = CreateItemsAcrossCollections();
        var session = CreateSession(playerItems);
        sessions.Set(connection, session);
        var expectedMatches = new (uint Uid, uint ItemId, string Collection)[]
        {
            (101, 1_000_101, "inventory"),
            (102, 1_000_102, "relicFuse"),
            (103, 1_000_103, "mythsoulBag"),
            (104, 1_000_104, "equipment"),
            (105, 1_000_105, "rune"),
            (106, 1_000_106, "wardrobe"),
            (107, 1_000_107, "warehouse"),
            (108, 1_000_108, "inventory.deposited"),
        };

        foreach (var expected in expectedMatches)
        {
            log.GetStringBuilder().Clear();
            var request = CreateItemUsageFrame(
                actionId: 0xCAFE,
                id: expected.Uid,
                dwParam: 0,
                timestamp: Timestamp,
                dwParam2: 0,
                dwParam3: 0,
                dwParam4: 0,
                payloadLength: MsgItemUsagePacket.PayloadLength);

            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    Sessions: sessions,
                    PlayerItemsRoot: _itemsRoot),
                ReadFrame(request));

            Assert.Empty(sent);
            var evidence = Assert.Single(WireEvidenceLines(log.ToString()));
            Assert.Contains(
                $"id={expected.Uid}->{expected.Collection}(uid={expected.Uid},itemId={expected.ItemId},",
                evidence,
                StringComparison.Ordinal);
            Assert.True(sessions.TryGet(connection, out var sessionAfter));
            Assert.Same(session, sessionAfter);
        }
    }

    [Fact]
    public async Task OversizedUnsupportedPayloadCapsHexAndFingerprintsTheFullPayload()
    {
        const int oversizedPayloadLength = 512;
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(PlayerItemSnapshot.Empty);
        var request = CreateItemUsageFrame(
            actionId: 0xBEEF,
            id: 999_001,
            dwParam: 0,
            timestamp: Timestamp,
            dwParam2: 0,
            dwParam3: 0,
            dwParam4: 0,
            payloadLength: oversizedPayloadLength);
        var payload = request.AsSpan(PacketHeader.Size).ToArray();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            ReadFrame(request));

        Assert.Empty(sent);
        var evidence = Assert.Single(WireEvidenceLines(log.ToString()));
        Assert.Contains(
            $"payloadLength={oversizedPayloadLength} payloadHexBytes={PayloadHexLimit} payloadHexTruncated=true",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains(
            $"payloadHex={Convert.ToHexString(payload.AsSpan(0, PayloadHexLimit))}",
            evidence,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(payload), evidence, StringComparison.Ordinal);
        Assert.Contains($"fullPayloadSha256={Sha256(payload)}", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupportedItemUsageActionDoesNotEmitWireEvidence()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateItemUsageEvidencePlayerItems());
        var request = new MsgItemUsagePacket(
            Id: EquipmentUid,
            DwParam: 0,
            MsgItemUsageAction.Ping,
            Timestamp,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            ReadFrame(request));

        Assert.Equal(request, Assert.Single(sent));
        Assert.Empty(WireEvidenceLines(log.ToString()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "wire-tester",
            Character: CreateCharacterSnapshot(),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot,
            ActiveNpcId: ActiveNpcId);
    }

    private CharacterSnapshot CreateCharacterSnapshot()
    {
        return new CharacterSnapshot(
            Uid: CharacterUid,
            Name: "WireTester",
            Body: 1003,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 123_456,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 100,
            Agility: 100,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 123_456,
            ConquerPoints: 654_321,
            BoundConquerPoints: 111,
            BankConquerPoints: 222,
            PkPoints: 0,
            Online: 7,
            SourceFile: _characterSnapshotPath,
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static PlayerItemSnapshot CreateItemUsageEvidencePlayerItems()
    {
        var referenced = CreateItem(ReferencedItemUid, ReferencedItemId);
        var equipment = CreateItem(
            EquipmentUid,
            EquipmentItemId,
            position: 4,
            animaItemId: ReferencedItemId);
        return new PlayerItemSnapshot(
            InventoryItems: [referenced],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [equipment],
            RuneItems: [],
            WardrobeItems: [])
        {
            WarehouseItems = [],
        };
    }

    private static PlayerItemSnapshot CreateItemsAcrossCollections()
    {
        var deposited = CreateItem(108, 1_000_108);
        var inventory = CreateItem(101, 1_000_101, depositedItems: [deposited]);
        return new PlayerItemSnapshot(
            InventoryItems: [inventory],
            RelicFuseItems: [CreateItem(102, 1_000_102)],
            MythsoulBagItems: [CreateItem(103, 1_000_103)],
            EquipmentItems: [CreateItem(104, 1_000_104, position: 4)],
            RuneItems: [CreateItem(105, 1_000_105, position: 301)],
            WardrobeItems: [CreateItem(106, 1_000_106)])
        {
            WarehouseItems = [CreateItem(107, 1_000_107, warehouseId: 8)],
        };
    }

    private static PlayerItemSnapshotItem CreateItem(
        uint uid,
        uint itemId,
        ushort position = 0,
        uint animaItemId = 0,
        uint warehouseId = 0,
        IReadOnlyList<PlayerItemSnapshotItem>? depositedItems = null)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = position,
            AnimaItemId = animaItemId,
            WarehouseId = warehouseId,
            StackSize = 1,
            DepositedItems = depositedItems ?? [],
        };
    }

    private static byte[] CreateItemUsageFrame(
        ushort actionId,
        uint id,
        ulong dwParam,
        uint timestamp,
        uint dwParam2,
        uint dwParam3,
        uint dwParam4,
        int payloadLength)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, payloadLength);
        writer.WriteUInt32(0, id);
        writer.WriteUInt64(4, dwParam);
        writer.WriteUInt16(12, actionId);
        writer.WriteUInt32(14, timestamp);
        writer.WriteUInt32(18, dwParam2);
        writer.WriteUInt32(22, dwParam3);
        writer.WriteUInt32(26, dwParam4);
        for (var offset = MsgItemUsagePacket.MinimumPayloadLength; offset < payloadLength; offset++)
        {
            writer.Payload[offset] = checked((byte)((offset % 251) + 1));
        }

        return writer.ToArray();
    }

    private static IReadOnlyList<string> WireEvidenceLines(string output)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(WireEvidencePrefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static ConquerTcpConnection CreateConnection(ICollection<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
