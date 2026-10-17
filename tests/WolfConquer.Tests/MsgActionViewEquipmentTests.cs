using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgActionViewEquipmentTests
{
    private const uint RequesterUid = 1_293_600;
    private const uint TargetUid = 1_293_601;
    private const uint EquipmentItemUid = 500_101;
    private const uint EquipmentItemId = 500_109;
    private const uint RuneItemUid = 500_102;
    private const uint RuneItemId = 403_6205;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task DispatchViewEquipmentSendsLegacyFanoutAndTargetTalk()
    {
        using var log = new StringWriter();
        var requesterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var requesterConnection = CreateConnection(requesterSent);
        var targetConnection = CreateConnection(targetSent);
        var requester = CreateSession(RequesterUid, "Requester");
        var target = CreateSession(
            TargetUid,
            "VisibleTarget",
            CreateTargetItems(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Spouse"] = "TargetSpouse",
                ["PrestigePoints"] = string.Join('/', Enumerable.Range(1, 43)),
                ["HundredWeaponsScore"] = "2345",
                ["PerfectionRedRune"] = "101",
                ["PerfectionBlueRune"] = "202",
                ["PerfectionYellowRune"] = "303",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(requesterConnection, requester);
        sessions.Set(targetConnection, target);
        var context = new WorldPacketHandlerContext(
            requesterConnection,
            log,
            requester,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(CreateViewEquipmentAction(TargetUid).ToFrameBytes()));

        Assert.Equal(5, requesterSent.Count);
        var ability = Assert.Single(ParseAbilityScores(requesterSent));
        Assert.Equal(LegacyUserAbilityScorePacket.ResponseType, ability.Type);
        Assert.Equal(TargetUid, ability.Uid);
        Assert.Equal((uint)target.Character.Level, ability.Level);
        Assert.Equal(43, ability.Items.Count);
        Assert.Equal(new LegacyUserAbilityScoreEntry(1, 1), ability.Items[0]);
        Assert.Equal(new LegacyUserAbilityScoreEntry(43, 43), ability.Items[42]);

        var itemViews = ParseItemViews(requesterSent);
        var equipmentView = Assert.Single(itemViews, packet => packet.Mode == LegacyItemViewPacket.ViewEquipMode);
        Assert.Equal(TargetUid, equipmentView.PlayerUid);
        Assert.Equal(EquipmentItemUid, equipmentView.Uid);
        Assert.Equal(EquipmentItemId, equipmentView.ItemId);
        Assert.Equal((byte)4, equipmentView.Position);

        var runeView = Assert.Single(itemViews, packet => packet.Mode == LegacyItemViewPacket.RuneMode);
        Assert.Equal(TargetUid, runeView.PlayerUid);
        Assert.Equal(RuneItemUid, runeView.Uid);
        Assert.Equal(RuneItemId, runeView.ItemId);
        Assert.Equal((byte)101, runeView.Position);

        var spouse = Assert.Single(ParseStrings(requesterSent), packet => packet.Id == LegacyStringId.ViewEquipSpouse);
        Assert.Equal(RequesterUid, spouse.Uid);
        Assert.Equal("TargetSpouse", Assert.Single(spouse.Strings));

        var status = Assert.Single(ParseStatuses(requesterSent));
        Assert.Equal(TargetUid, status.Uid);
        Assert.Equal((uint)target.Character.MinMana, status.MaxMana);
        Assert.True(status.MaxHP >= target.Character.MinHitPoints);

        var targetMessage = Assert.Single(ParseMessages(targetSent));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, targetMessage.ChatType);
        Assert.Equal("Requester is checking your equipment.", targetMessage.Content[3]);

        var output = log.ToString();
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
    }

    [Fact]
    public async Task DispatchViewEquipmentMissingTargetIsSourceSilent()
    {
        using var log = new StringWriter();
        var requesterSent = new List<byte[]>();
        var requesterConnection = CreateConnection(requesterSent);
        var requester = CreateSession(RequesterUid, "Requester");
        var sessions = new WorldSessionStore();
        sessions.Set(requesterConnection, requester);
        var context = new WorldPacketHandlerContext(
            requesterConnection,
            log,
            requester,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(CreateViewEquipmentAction(TargetUid).ToFrameBytes()));

        Assert.Empty(requesterSent);
        var output = log.ToString();
        Assert.DoesNotContain("PARITY-GAP category=action severity=warning gap=registered-msgaction", output);
        Assert.DoesNotContain("PARITY-GAP registered MsgAction", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("gap=movement-action", output);
    }

    private static MsgActionPacket CreateViewEquipmentAction(uint targetUid)
    {
        return new MsgActionPacket
        {
            ObjId = RequesterUid,
            DwParam = targetUid,
            Timestamp = 123_456,
            Type = MsgActionType.ViewEquipment,
            PositionX = 835,
            PositionY = 596,
        };
    }

    private static PlayerItemSnapshot CreateTargetItems()
    {
        var equipment = new PlayerItemSnapshotItem
        {
            Uid = EquipmentItemUid,
            ItemId = EquipmentItemId,
            Durability = 120,
            MaximumDurability = 150,
            Position = 4,
            SocketProgress = 42,
            SocketOne = 13,
            SocketTwo = 3,
            Plus = 6,
            Bless = 1,
            StackSize = 1,
            ItemPoints = 777,
        };
        var rune = new PlayerItemSnapshotItem
        {
            Uid = RuneItemUid,
            ItemId = RuneItemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 101,
            StackSize = 1,
            RuneExperience = 12_345,
            Attribute1 = 40_007,
        };

        return new PlayerItemSnapshot([], [], [], [equipment], [rune]);
    }

    private static WorldSession CreateSession(
        uint uid,
        string name,
        PlayerItemSnapshot? playerItems = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var characterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (attributes is not null)
        {
            foreach (var pair in attributes)
            {
                characterAttributes[pair.Key] = pair.Value;
            }
        }

        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 132,
                Experience: 0,
                MapId: 1015,
                X: 835,
                Y: 596,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 40,
                Agility: 36,
                Vitality: 120,
                Spirit: 20,
                AttributePoints: 0,
                MinHitPoints: 2_500,
                MinMana: 600,
                Money: 10_000,
                ConquerPoints: 500,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "",
                Attributes: characterAttributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: Path.GetTempPath());
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
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

    private static IReadOnlyList<LegacyUserAbilityScorePacket> ParseAbilityScores(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyUserAbilityScorePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyUserAbilityScorePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyItemViewPacket> ParseItemViews(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyItemViewPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyItemViewPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyStringPacket> ParseStrings(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyPlayerStatusPacket> ParseStatuses(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyPlayerStatusPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
