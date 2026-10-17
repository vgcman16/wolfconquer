using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArthurVipQuestNpcTests
{
    private const uint ArthurNpcId = 3_601;

    [Fact]
    public async Task HandlerSendsArthurVipQuestExchangeDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(ArthurNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(14, sent.Count);
        Assert.Contains("This place can be quite dangerous", AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Contains("Stage 4", AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Equal(1, AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Option).OptionId);
        Assert.Equal(5, AssertNpcReply(sent[11], LegacyNpcReplyInteractType.Option).OptionId);
        AssertNpcReply(sent[^1], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Arthur", log.ToString());
    }

    [Fact]
    public async Task HandlerArthurStageTwoRejectsMissingGhostHornsWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(20, 722_722, 2)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(ArthurNpcId, optionId: 2, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 722_721U);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text.Contains("need at least 3 GhostHorn", StringComparison.Ordinal));
        Assert.Contains("reason=missing-materials", log.ToString());
    }

    [Fact]
    public async Task HandlerArthurExitTeleportsToTwinCity()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(ArthurNpcId, optionId: 5, interactType: 1).ToFrameBytes()));

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var action));
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(1_002U, action.DwParam);
        Assert.Equal(410U, action.PositionX);
        Assert.Equal(355U, action.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(355, updated.Character.Y);
        Assert.Contains("source=NpcHandler.Arthur", log.ToString());
    }

    private static WorldSession CreateSession(IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "arthur-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "VipSeeker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 11_127,
                X: 208,
                Y: 255,
                PreviousMapId: 11_127,
                PreviousMapX: 208,
                PreviousMapY: 255,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = stackSize,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
