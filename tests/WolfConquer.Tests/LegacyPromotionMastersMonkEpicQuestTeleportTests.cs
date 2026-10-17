using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkEpicQuestTeleportTests
{
    private const uint MonkPromotionNpcId = 8_314;
    private const byte GoldenCicadaTeleportOption = 91;
    private const long CharacterUid = 1_293_602;

    [Theory]
    [InlineData(69, 2)]
    [InlineData(70, 1)]
    public async Task Option91UnderGateSendsSourceDialogWithoutTeleport(long level, long reborn)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: level, reborn: reborn);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            MonkPromotionNpcId,
            GoldenCicadaTeleportOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(430, updated.Character.X);
        Assert.Equal(378, updated.Character.Y);
        Assert.DoesNotContain(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Please come back when you will be 2nd Reborn and with a higher level than 70.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
                reply.Text == "Okay.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 226);

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionMonk Golden Cicada teleport", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:43264-43278", logText);
        Assert.Contains("option=91", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option91Level70SecondRebornTeleportsToGoldenCicadaMapAndUpdatesSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 70, reborn: 2);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            MonkPromotionNpcId,
            GoldenCicadaTeleportOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_983, updated.Character.MapId);
        Assert.Equal(89, updated.Character.X);
        Assert.Equal(71, updated.Character.Y);
        Assert.Equal(1_002, updated.Character.PreviousMapId);
        Assert.Equal(430, updated.Character.PreviousMapX);
        Assert.Equal(378, updated.Character.PreviousMapY);

        Assert.Empty(ParseNpcReplies(sent));
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 3_983, expectedX: 89, expectedY: 71);

        var logText = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpc teleport transition", logText);
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk Golden Cicada teleport", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:43264-43278", logText);
        Assert.Contains("option=91", logText);
        Assert.Contains("map=3983 x=89 y=71", logText);
    }

    private static WorldSession CreateSession(long level, long reborn)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "monk-epic-quest-teleport-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MonkHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 6_004,
                FirstClass: 6_004,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
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
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
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

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(sent.Count >= startIndex + 5);
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
