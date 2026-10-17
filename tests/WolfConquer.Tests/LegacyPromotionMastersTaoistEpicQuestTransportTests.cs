using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistEpicQuestTransportTests
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const long CharacterUid = 1_293_761;
    private const long StartMapId = 1_002;
    private const long StartX = 430;
    private const long StartY = 378;
    private const long SunKingMapId = 3_933;
    private const long SunKingX = 84;
    private const long SunKingY = 84;

    [Theory]
    [InlineData(69, 2)]
    [InlineData(70, 1)]
    public async Task TaoistEpicQuestTransportOptionRejectsBeforeSourceLevelAndRebornGates(
        long level,
        long reborn)
    {
        var result = await InvokeTaoistEpicQuestTransportAsync(level, reborn);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(StartMapId, updated.Character.MapId);
        Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);

        AssertDialogWithAvatar(
            ParseNpcReplies(result.Sent),
            "Please come back when you will be 2nd Reborn and with a higher level than 70.",
            "Okay.",
            6);

        Assert.Contains("WorldServer skipped LegacyNpc PromotionTaoist epic quest transport", result.Log);
        Assert.Contains("source=NpcHandler.PromotionTaoist", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:41461-41475", result.Log);
        Assert.Contains("option=191", result.Log);
        Assert.Contains("level=" + level, result.Log);
        Assert.Contains("requiredLevel=70", result.Log);
        Assert.Contains("reborn=" + reborn, result.Log);
        Assert.Contains("requiredReborn=2", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task TaoistEpicQuestTransportOptionTeleportsSecondRebornLevelSeventyToSunKing()
    {
        var result = await InvokeTaoistEpicQuestTransportAsync(level: 70, reborn: 2);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(SunKingMapId, updated.Character.MapId);
        Assert.Equal(SunKingX, updated.Character.X);
        Assert.Equal(SunKingY, updated.Character.Y);
        Assert.Equal(StartMapId, updated.Character.PreviousMapId);
        Assert.Equal(StartX, updated.Character.PreviousMapX);
        Assert.Equal(StartY, updated.Character.PreviousMapY);

        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal((uint)SunKingMapId, teleport.DwParam);
        Assert.Equal(SunKingMapId, teleport.DwParam3);
        Assert.Equal((uint)SunKingX, teleport.PositionX);
        Assert.Equal((uint)SunKingY, teleport.PositionY);
        Assert.Empty(ParseNpcReplies(result.Sent));

        Assert.Contains("WorldServer sent LegacyNpc teleport transition", result.Log);
        Assert.Contains("WorldServer handled LegacyNpc PromotionTaoist epic quest transport", result.Log);
        Assert.Contains("source=NpcHandler.PromotionTaoist", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:41461-41475", result.Log);
        Assert.Contains("option=191", result.Log);
        Assert.Contains("map=3933 x=84 y=84", result.Log);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", result.Log);
        Assert.DoesNotContain("source gate rejected", result.Log);
    }

    private static async Task<TaoistEpicQuestTransportInvokeResult> InvokeTaoistEpicQuestTransportAsync(
        long level,
        long reborn)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level, reborn);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TaoistPromotionNpcId,
            optionId: 191,
            CancellationToken.None);

        return new TaoistEpicQuestTransportInvokeResult(
            handled,
            sent,
            connection,
            sessions,
            log.ToString());
    }

    private static WorldSession CreateSession(long level, long reborn)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "taoist-epic-quest-transport-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TaoistHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 13_002,
                FirstClass: 13_002,
                SecondClass: 13_002,
                Reborn: reborn,
                Level: level,
                Experience: 0,
                MapId: StartMapId,
                X: StartX,
                Y: StartY,
                PreviousMapId: StartMapId,
                PreviousMapX: StartX,
                PreviousMapY: StartY,
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
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: Path.GetTempPath());
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertDialogWithAvatar(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string expectedText,
        string expectedOptionText,
        ushort expectedAvatarId)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == expectedOptionText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == expectedAvatarId);
    }

    private sealed record TaoistEpicQuestTransportInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        string Log);
}
