using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildGateNavigationNpcTests
{
    [Fact]
    public async Task HandlerSendsSourceBackedGuildGateKeeperDialog()
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
            ReadFrame(LegacyNpcServerRequestPacket.Script(7_000, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Are you going to leave here?", dialog.Text);

        var yes = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yes.OptionId);
        Assert.Equal("Yes.", yes.Text);

        var no = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, no.OptionId);
        Assert.Equal("No.~Wait~a~moment.", no.Text);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(7, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.Equal(7_000U, activeSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.GuildGateKeeper", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForGuildGateKeeperReplyTeleport()
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
            ReadFrame(LegacyNpcServerRequestPacket.Script(7_000, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(11, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 5, session, expectedMapId: 1_002, expectedX: 410, expectedY: 354);
        Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(sent[10]), out var equipmentRefresh));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                session.PlayerItems,
                session.AlternateEquipmentActive),
            equipmentRefresh);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1_002, teleportedSession.Character.MapId);
        Assert.Equal(410, teleportedSession.Character.X);
        Assert.Equal(354, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.GuildGateKeeper", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_038,
                X: 349,
                Y: 340,
                PreviousMapId: 1_038,
                PreviousMapX: 349,
                PreviousMapY: 340,
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
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType, uint npcId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, npcId);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        AssertLegacyNpcTeleportAction(sent[startIndex], session, expectedMapId, expectedX, expectedY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 1]), out var merchantStartup));
        Assert.Equal((uint)session.Character.Uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);
        Assert.Equal(expectedX, merchantStartup.PositionX);
        Assert.Equal(expectedY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var stopVending));
        Assert.Equal((uint)session.Character.Uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var color));
        Assert.Equal((uint)session.Character.Uid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[startIndex + 4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, mapsById: null), mapStatus);
    }

    private static void AssertLegacyNpcTeleportAction(
        byte[] bytes,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
        Assert.Equal((long)expectedMapId, action.DwParam3);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
