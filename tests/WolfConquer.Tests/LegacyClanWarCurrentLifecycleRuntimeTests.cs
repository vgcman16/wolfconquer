using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanWarCurrentLifecycleRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const string TwinClanWarInvitationText = "TwinCity Clan War";
    private const string TwinClanWarInvitationMessage =
        " " + TwinClanWarInvitationText + " is about to begin! Will you join it? Prize[Conquer Points]";
    private const string PhoenixClanWarInvitationText = "PhoenixCity Clan War";
    private const string PhoenixClanWarInvitationMessage =
        " " + PhoenixClanWarInvitationText + " is about to begin! Will you join it? Prize[Conquer Points]";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task RuntimeOpensTwinClanWarGlobalWarOnSourceStartSecondWithInvitationPrompt()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sourceSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var sourceSession = CreateSession(
            playerUid: PlayerUid,
            mapId: 1_002,
            name: "Source",
            CreateDormantGlobalClanWarAttributes("Twin", "SourceClan", "501"));
        var viewerSession = CreateSession(
            playerUid: PlayerUid + 1,
            mapId: 1_002,
            name: "Viewer",
            CreateDormantGlobalClanWarAttributes("Twin", "ViewerClan", "502"));
        var sessions = new WorldSessionStore();
        sessions.Set(sourceConnection, sourceSession);
        sessions.Set(viewerConnection, viewerSession);

        var sentCount = await WorldPacketHandlers.SendDueLegacyClanWarCurrentOpenWarFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 23, 1, 1, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(4, sentCount);
        AssertClanWarInvitationSent(sourceSent, TwinClanWarInvitationMessage);
        AssertClanWarInvitationSent(viewerSent, TwinClanWarInvitationMessage);
        AssertOpenedClanWar(sessions, sourceConnection, "Twin", "1002", "327", "138");
        AssertOpenedClanWar(sessions, viewerConnection, "Twin", "1002", "327", "138");

        var output = log.ToString();
        Assert.Contains("source MsgClanWar.Open/CityWar.Open modeled", output);
        Assert.Contains("TwinCity Clan War", output);
        Assert.Contains("startsGlobalWar=True", output);
    }

    [Fact]
    public async Task RuntimeSkipsTwinClanWarGlobalWarAfterSourceStartSecondWindow()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            playerUid: PlayerUid,
            mapId: 1_002,
            name: "Source",
            CreateDormantGlobalClanWarAttributes("Twin", "SourceClan", "501"));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var sentCount = await WorldPacketHandlers.SendDueLegacyClanWarCurrentOpenWarFramesAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 23, 1, 2, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(0, sentCount);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var currentSession));
        Assert.NotNull(currentSession);
        Assert.Equal("Dead", currentSession.Character.Attributes["LegacyClanWarProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["ClanWarProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["LegacyClanWarCurrentProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["ClanWarCurrentProcess"]);
        Assert.False(currentSession.Character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
    }

    [Fact]
    public async Task RuntimeOpensPhoenixClanWarCurrentCityOnSourceMinuteWithAliveProcessAndInvitationPrompt()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sourceSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var sourceSession = CreateSession(
            playerUid: PlayerUid,
            mapId: 1_002,
            name: "Source",
            CreateDormantCurrentClanWarAttributes("Twin", "SourceClan", "501"));
        var viewerSession = CreateSession(
            playerUid: PlayerUid + 1,
            mapId: 1_002,
            name: "Viewer",
            CreateDormantCurrentClanWarAttributes("Twin", "ViewerClan", "502"));
        var sessions = new WorldSessionStore();
        sessions.Set(sourceConnection, sourceSession);
        sessions.Set(viewerConnection, viewerSession);

        await WorldPacketHandlers.SendDueLegacyClanWarCurrentOpenWarFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 12, 11, 0, TimeSpan.Zero),
            CancellationToken.None);

        AssertClanWarInvitationSent(sourceSent, PhoenixClanWarInvitationMessage);
        AssertClanWarInvitationSent(viewerSent, PhoenixClanWarInvitationMessage);
        AssertOpenedClanWar(sessions, sourceConnection, "Phoenix", "1011", "210", "260");
        AssertOpenedClanWar(sessions, viewerConnection, "Phoenix", "1011", "210", "260");

        var output = log.ToString();
        Assert.Contains("source MsgClanWar.Open/CityWar.Open modeled", output);
        Assert.Contains("PhoenixCity Clan War", output);
        Assert.Contains("Conquer Points", output);
    }

    [Fact]
    public async Task RuntimeSkipsClanWarCurrentOpenWarOutsideSourceStartMinute()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            playerUid: PlayerUid,
            mapId: 1_002,
            name: "Source",
            CreateDormantCurrentClanWarAttributes("Twin", "SourceClan", "501"));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var sentCount = await WorldPacketHandlers.SendDueLegacyClanWarCurrentOpenWarFramesAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 12, 12, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(0, sentCount);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var currentSession));
        Assert.NotNull(currentSession);
        Assert.Equal("Twin", currentSession.Character.Attributes["LegacyClanWarCurrentCity"]);
        Assert.Equal("Twin", currentSession.Character.Attributes["ClanWarCurrentCity"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["LegacyClanWarCurrentProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["ClanWarCurrentProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["LegacyClanWarCityProcess"]);
        Assert.Equal("Dead", currentSession.Character.Attributes["ClanWarCityProcess"]);
        Assert.False(currentSession.Character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
    }

    [Fact]
    public async Task HandlerLegacyNpcReplyAcceptsPendingClanWarCurrentInvitationPrompt()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            playerUid: PlayerUid,
            mapId: 1_002,
            name: "Source",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [LegacyRuntimeMessageBoxPrompt.PendingAttribute] = "ClanWarInvitation:Phoenix",
                [LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute] = "1011",
                [LegacyRuntimeMessageBoxPrompt.DestinationXAttribute] = "210",
                [LegacyRuntimeMessageBoxPrompt.DestinationYAttribute] = "260",
                [LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute] = "0",
                [LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute] = "60",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_011, updated.Character.MapId);
        Assert.Equal(210, updated.Character.X);
        Assert.Equal(260, updated.Character.Y);
        Assert.Equal(1_002, updated.Character.PreviousMapId);
        Assert.Equal(100, updated.Character.PreviousMapX);
        Assert.Equal(100, updated.Character.PreviousMapY);
        Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
        Assert.Equal("0", updated.Character.Attributes["PDinamycID"]);
        AssertNoPendingMessageBoxAttributes(updated.Character);
        AssertLegacyTeleportTransition(sent, updated, expectedMapId: 1_011, expectedX: 210, expectedY: 260);
        Assert.Contains("accepted legacy ClanWar current invitation message box", log.ToString());
    }

    [Fact]
    public async Task RuntimeCompletesTwinClanWarCurrentCityOnSourceMinuteWithRedCenterClaimAndDeadProcess()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sourceSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var sourceSession = CreateSession(
            playerUid: PlayerUid,
            mapId: LegacySobNpcEventRules.TwinCityClanWarMapId,
            name: "Source",
            CreateClanWarAttributes("Twin", "SourceClan", "501"));
        var viewerSession = CreateSession(
            playerUid: PlayerUid + 1,
            mapId: 1_002,
            name: "Viewer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var sessions = new WorldSessionStore();
        sessions.Set(sourceConnection, sourceSession);
        sessions.Set(viewerConnection, viewerSession);

        var sentCount = await WorldPacketHandlers.SendDueLegacyClanWarCurrentCompleteWarFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 12, 10, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(2, sentCount);
        AssertClaimMessage(sourceSent, "SourceClanhas claimed the prize for domination of TwinCityArena.");
        AssertClaimMessage(viewerSent, "SourceClanhas claimed the prize for domination of TwinCityArena.");

        Assert.True(sessions.TryGet(sourceConnection, out var completedSession));
        Assert.NotNull(completedSession);
        Assert.Equal("Dead", completedSession.Character.Attributes["LegacyClanWarCurrentProcess"]);
        Assert.Equal("Dead", completedSession.Character.Attributes["ClanWarCurrentProcess"]);
        Assert.Equal("Dead", completedSession.Character.Attributes["LegacyClanWarCityProcess"]);
        Assert.Equal("Twin", completedSession.Character.Attributes["LegacyClanWarLastCompletedCity"]);
        Assert.Equal("SourceClan", completedSession.Character.Attributes["LegacyClanWarWinnerClanName"]);
        Assert.Equal("501", completedSession.Character.Attributes["LegacyClanWarWinnerClanID"]);
        Assert.Equal("1", completedSession.Character.Attributes["LegacyClanWarWinnerOccupationDays"]);
        Assert.Equal("722454", completedSession.Character.Attributes["LegacyClanWarWinnerRewardItemID"]);
        Assert.False(completedSession.Character.Attributes.ContainsKey("ClanWarScores"));

        Assert.True(sessions.TryGet(viewerConnection, out var unchangedViewerSession));
        Assert.NotNull(unchangedViewerSession);
        Assert.Empty(unchangedViewerSession.Character.Attributes);
        Assert.Contains("source MsgClanWar.CityWar.CompleteWar modeled with economy-safe MsgClanWar.CityWar.GetRewards payout", log.ToString());
    }

    [Fact]
    public async Task RuntimeSkipsClanWarCurrentCompleteWarOutsideSourceCompletionMinute()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            playerUid: PlayerUid,
            mapId: LegacySobNpcEventRules.TwinCityClanWarMapId,
            name: "Source",
            CreateClanWarAttributes("Twin", "SourceClan", "501"));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var sentCount = await WorldPacketHandlers.SendDueLegacyClanWarCurrentCompleteWarFramesAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions),
            new DateTimeOffset(2026, 6, 30, 12, 9, 59, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(0, sentCount);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var currentSession));
        Assert.NotNull(currentSession);
        Assert.Equal("Alive", currentSession.Character.Attributes["LegacyClanWarCurrentProcess"]);
    }

    private static Dictionary<string, string> CreateDormantCurrentClanWarAttributes(
        string city,
        string winnerClanName,
        string winnerClanId)
    {
        var attributes = CreateClanWarAttributes(city, winnerClanName, winnerClanId);
        attributes["LegacyClanWarCurrentProcess"] = "Dead";
        attributes["ClanWarCurrentProcess"] = "Dead";
        attributes["LegacyClanWarCurrentProces"] = "Dead";
        attributes["ClanWarCurrentProces"] = "Dead";
        attributes["LegacyClanWarCityProcess"] = "Dead";
        attributes["ClanWarCityProcess"] = "Dead";
        attributes["LegacyClanWarCityProces"] = "Dead";
        attributes["ClanWarCityProces"] = "Dead";
        attributes.Remove("ClanWarScores");
        return attributes;
    }

    private static Dictionary<string, string> CreateDormantGlobalClanWarAttributes(
        string city,
        string winnerClanName,
        string winnerClanId)
    {
        var attributes = CreateDormantCurrentClanWarAttributes(city, winnerClanName, winnerClanId);
        attributes["LegacyClanWarProcess"] = "Dead";
        attributes["ClanWarProcess"] = "Dead";
        attributes["RuntimeClanWarProcess"] = "Dead";
        attributes["LegacyClanWarProces"] = "Dead";
        attributes["ClanWarProces"] = "Dead";
        attributes["RuntimeClanWarProces"] = "Dead";
        return attributes;
    }

    private static Dictionary<string, string> CreateClanWarAttributes(
        string city,
        string winnerClanName,
        string winnerClanId)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = winnerClanId,
            ["ClanUID"] = winnerClanId,
            ["ClanName"] = winnerClanName,
            ["LegacyClanName"] = winnerClanName,
            ["LegacyClanWarProcess"] = "Alive",
            ["ClanWarProcess"] = "Alive",
            ["LegacyClanWarProces"] = "Alive",
            ["ClanWarProces"] = "Alive",
            ["LegacyClanWarCurrentProcess"] = "Alive",
            ["ClanWarCurrentProcess"] = "Alive",
            ["LegacyClanWarCurrentProces"] = "Alive",
            ["ClanWarCurrentProces"] = "Alive",
            ["LegacyClanWarCityProcess"] = "Alive",
            ["ClanWarCityProcess"] = "Alive",
            ["LegacyClanWarCityProces"] = "Alive",
            ["ClanWarCityProces"] = "Alive",
            ["LegacyClanWarCurrentCity"] = city,
            ["ClanWarCurrentCity"] = city,
            ["LegacyClanWarWinnerClanName"] = winnerClanName,
            ["ClanWarWinnerClanName"] = winnerClanName,
            ["LegacyClanWarWinnerClanID"] = winnerClanId,
            ["ClanWarWinnerClanID"] = winnerClanId,
            ["ClanWarScores"] = $"{city},{winnerClanId},5000,{Uri.EscapeDataString(winnerClanName)}",
        };
    }

    private static WorldSession CreateSession(
        long playerUid,
        int mapId,
        string name,
        IReadOnlyDictionary<string, string> attributes)
    {
        var character = new CharacterSnapshot(
            Uid: playerUid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: 100,
            Y: 100,
            PreviousMapId: mapId,
            PreviousMapX: 100,
            PreviousMapY: 100,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://legacy-clan-war-current-lifecycle",
            Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>());

        return new WorldSession(
            AccountId: playerUid,
            Username: name.ToLowerInvariant(),
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
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

    private static void AssertClanWarInvitationSent(IReadOnlyList<byte[]> sent, string expectedMessage)
    {
        Assert.Contains(sent, bytes => IsClanWarSystemCenter(bytes, expectedMessage));
        Assert.Contains(sent, bytes => IsClanWarInvitationMessageBox(bytes, expectedMessage));
    }

    private static bool IsClanWarSystemCenter(byte[] bytes, string expectedMessage)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Content.Contains(expectedMessage);
    }

    private static bool IsClanWarInvitationMessageBox(byte[] bytes, string expectedMessage)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
            reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            reply.Text == expectedMessage;
    }

    private static void AssertOpenedClanWar(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        string city,
        string mapId,
        string x,
        string y)
    {
        Assert.True(sessions.TryGet(connection, out var openedSession));
        Assert.NotNull(openedSession);
        Assert.Equal(city, openedSession.Character.Attributes["LegacyClanWarCurrentCity"]);
        Assert.Equal(city, openedSession.Character.Attributes["ClanWarCurrentCity"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["LegacyClanWarProcess"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["ClanWarProcess"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["LegacyClanWarCurrentProcess"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["ClanWarCurrentProcess"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["LegacyClanWarCityProcess"]);
        Assert.Equal("Alive", openedSession.Character.Attributes["ClanWarCityProcess"]);
        Assert.False(string.IsNullOrWhiteSpace(
            openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.PendingAttribute]));
        Assert.Equal(mapId, openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute]);
        Assert.Equal(x, openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationXAttribute]);
        Assert.Equal(y, openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationYAttribute]);
        Assert.Equal("0", openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute]);
        Assert.Equal("60", openedSession.Character.Attributes[LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute]);
    }

    private static void AssertClaimMessage(IEnumerable<byte[]> sent, string expected)
    {
        var message = Assert.Single(ParseLegacyMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, message.Color);
        Assert.Equal(LegacyMsgMessagePacket.CenterChatType, message.ChatType);
        Assert.Contains(expected, message.Content);
    }

    private static void AssertLegacyTeleportTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal((uint)session.Character.Uid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal((long)expectedMapId, teleport.DwParam3);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal((uint)session.Character.Uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);
        Assert.Equal(expectedX, merchantStartup.PositionX);
        Assert.Equal(expectedY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal((uint)session.Character.Uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal((uint)session.Character.Uid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, mapsById: null), mapStatus);
    }

    private static void AssertNoPendingMessageBoxAttributes(CharacterSnapshot character)
    {
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationXAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationYAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute));
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseLegacyMessages(IEnumerable<byte[]> frames)
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

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }
}
