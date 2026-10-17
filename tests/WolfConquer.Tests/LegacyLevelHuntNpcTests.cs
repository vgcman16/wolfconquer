using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLevelHuntNpcTests
{
    public static IEnumerable<object[]> LevelHuntExitNpcs()
    {
        yield return [20_201U, "NpcHandler.leveHuntMap1"];
        yield return [20_202U, "NpcHandler.leveHuntMap2"];
        yield return [20_203U, "NpcHandler.leveHuntMap3"];
        yield return [20_204U, "NpcHandler.leveHuntMap4"];
        yield return [20_205U, "NpcHandler.leveHuntMap5"];
        yield return [20_206U, "NpcHandler.leveHuntMap6"];
    }

    [Theory]
    [MemberData(nameof(LevelHuntExitNpcs))]
    public async Task HandlerSendsSourceBackedIntroDialog(uint npcId, string expectedSource)
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
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Hey Brothr HeroOne Do You Want Leve this map to Twin City", dialog.Text);
        AssertAscii(dialog.Text);

        var yesOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yesOption.OptionId);
        Assert.Equal("Yes Plase", yesOption.Text);
        AssertAscii(yesOption.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("See~you~later.", closeOption.Text);
        AssertAscii(closeOption.Text);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(76, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        Assert.Contains($"source={expectedSource}", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [MemberData(nameof(LevelHuntExitNpcs))]
    public async Task HandlerAllowsSourceOptionOneTeleportToTwinCityWithoutLevelGate(
        uint npcId,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 1, interactType: 1).ToFrameBytes()));

        var teleport = Assert.Single(ParseTeleports(sent));
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Character.Level);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(355, updated.Character.Y);
        Assert.Equal(session.Character.MapId, updated.Character.PreviousMapId);
        Assert.Equal(session.Character.X, updated.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, updated.Character.PreviousMapY);
        Assert.Contains($"source={expectedSource}", log.ToString());
        Assert.DoesNotContain("clicked NPC is not currently visible", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForDialogReplyTeleport()
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
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_206, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        Assert.Equal(20_206U, activeSession.ActiveNpcId);

        var countBeforeReply = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        var teleport = Assert.Single(ParseTeleports(sent.Skip(countBeforeReply)));
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(355U, teleport.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(355, updated.Character.Y);
        Assert.Contains("source=NpcHandler.leveHuntMap6", log.ToString());
        Assert.DoesNotContain("clicked NPC is not currently visible", log.ToString());
    }

    [Theory]
    [InlineData(20, 1, "TC Captain")]
    [InlineData(40, 2, "PC Captain")]
    [InlineData(60, 3, "AC Captain")]
    [InlineData(80, 4, "DC Captain")]
    [InlineData(100, 5, "BI Captain")]
    [InlineData(110, 6, "MC Captain")]
    public async Task FrozenGrottoGeneralDialogUsesLegacyLevelRoute(
        int level,
        byte expectedOption,
        string expectedText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(6_144, optionId: 0, interactType: 1).ToFrameBytes()));

        var replies = sent
            .Select(AssertNpcReply)
            .ToArray();
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == expectedOption);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 57);
        Assert.All(replies.Select(reply => reply.Text), AssertAscii);
        Assert.Contains("source=NpcHandler.FrozenGrottoGeneral", log.ToString());
    }

    [Fact]
    public async Task FrozenGrottoGeneralLevelOneHundredTwentyUsesCloudSaintCloseDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 120);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(6_144, optionId: 0, interactType: 1).ToFrameBytes()));

        var replies = sent
            .Select(AssertNpcReply)
            .ToArray();
        Assert.Contains(replies, reply => reply.Text.Contains("Cloud Saint", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == byte.MaxValue);
        Assert.DoesNotContain(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId is >= 1 and <= 6);
        Assert.All(replies.Select(reply => reply.Text), AssertAscii);
        Assert.Contains("source=NpcHandler.FrozenGrottoGeneral", log.ToString());
    }

    [Theory]
    [InlineData(1, 1_002U, 311U, 346U)]
    [InlineData(2, 1_011U, 233U, 260U)]
    [InlineData(3, 1_020U, 571U, 621U)]
    [InlineData(4, 1_000U, 483U, 634U)]
    [InlineData(5, 1_015U, 792U, 571U)]
    [InlineData(6, 1_000U, 85U, 321U)]
    public async Task FrozenGrottoGeneralTeleportsUseLegacyDestinations(
        byte optionId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
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
            ReadFrame(LegacyNpcServerRequestPacket.Script(6_144, optionId, interactType: 1).ToFrameBytes()));

        var teleport = Assert.Single(ParseTeleports(sent));
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal((int)expectedMapId, updated.Character.MapId);
        Assert.Equal((int)expectedX, updated.Character.X);
        Assert.Equal((int)expectedY, updated.Character.Y);
        Assert.Contains("source=NpcHandler.FrozenGrottoGeneral", log.ToString());
    }

    private static WorldSession CreateSession(int level = 140)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "level-hunt-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: 0,
                MapId: 10_342,
                X: 98,
                Y: 201,
                PreviousMapId: 1_002,
                PreviousMapX: 410,
                PreviousMapY: 355,
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

    private static IReadOnlyList<MsgActionPacket> ParseTeleports(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Type == MsgActionType.Teleport)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertAscii(string text)
    {
        Assert.All(text, character => Assert.True(character <= 127));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
