using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHourlyEventsNpcTests
{
    private const uint EventsInHourNpcId = 5_595;
    private const uint EventOutxx5NpcId = 199_151;
    private const long LegacyHourlyEventSourcePrizeConquerPoints = 500_000;
    private const long TunedLegacyHourlyEventPrizeConquerPoints = 50_000;

    [Fact]
    public async Task HandlerSendsTranslatedHourlyEventsMenuWithCurrentMinuteEntry()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventsInHourNpcId,
            optionId: 0,
            new DateTime(2026, 6, 20, 8, 31, 0),
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Hi. Welcome to [CoFreedom3D] events. Enjoy with us.");
        Assert.Contains(replies, reply => reply.Text == "Exchange tokens [Saturn]" && reply.OptionId == 55);
        Assert.Contains(replies, reply => reply.Text == "Saturn [Available]" && reply.OptionId == 40);
        Assert.Contains(replies, reply => reply.Text == "Top Fight [Available]" && reply.OptionId == 9);
        Assert.DoesNotContain(replies, reply => reply.Text == "Tomb [Available]");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.EventsInHour option=0", log.ToString());
        Assert.Contains("PARITY-GAP LegacyNpc HourlyEvents", log.ToString());
    }

    [Fact]
    public async Task HandlerTeleportsHourlyEntryWhenSourceMinuteGateIsOpen()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventsInHourNpcId,
            optionId: 9,
            new DateTime(2026, 6, 20, 8, 31, 0),
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_763U, teleport.DwParam);
        Assert.Equal(50U, teleport.PositionX);
        Assert.Equal(50U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_763, updated.Character.MapId);
        Assert.Equal(50, updated.Character.X);
        Assert.Equal(50, updated.Character.Y);
        Assert.Contains("source=NpcHandler.EventsInHour map=10763 x=50 y=50", log.ToString());
    }

    [Fact]
    public async Task HandlerTeleportsDragonWarHourlyEntryDuringSourceMinuteWindowAndClearsDynamicId()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 372, y: 362, dynamicId: "2468");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventsInHourNpcId,
            optionId: 4,
            new DateTime(2026, 6, 20, 8, 12, 0),
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_762U, teleport.DwParam);
        Assert.Equal(50U, teleport.PositionX);
        Assert.Equal(50U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_762, updated.Character.MapId);
        Assert.Equal(50, updated.Character.X);
        Assert.Equal(50, updated.Character.Y);
        Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
        Assert.Contains("source=NpcHandler.EventsInHour map=10762 x=50 y=50", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsExitConfirmationAndTeleportsBackToTwinCity()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 10_770, x: 50, y: 50);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);

        var confirmHandled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            context,
            session,
            EventOutxx5NpcId,
            optionId: 2,
            new DateTime(2026, 6, 20, 8, 6, 0),
            CancellationToken.None);

        Assert.True(confirmHandled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Do you want to teleport back to Twin City? Are you sure?");
        Assert.Contains(replies, reply => reply.Text == "Yes" && reply.OptionId == 3);

        sent.Clear();
        var exitHandled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            context,
            session,
            EventOutxx5NpcId,
            optionId: 3,
            new DateTime(2026, 6, 20, 8, 6, 0),
            CancellationToken.None);

        Assert.True(exitHandled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(354, updated.Character.Y);
        Assert.Contains("source=NpcHandler.EventOutxx5 map=1002 x=410 y=354", log.ToString());
    }

    [Fact]
    public async Task HandlerClaimsHourlyPrizeWithTunedConquerPointsWalletUpdatePersistenceAndSourceLog()
    {
        var dbRoot = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = WriteSnapshot(
                usersRoot,
                accountId: 100,
                uid: 1_293_600,
                conquerPoints: 12);
            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(
                mapId: 10_770,
                conquerPoints: 12,
                usersRoot: usersRoot);
            var sessions = new WorldSessionStore();
            sessions.Set(connection, session);

            var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                EventOutxx5NpcId,
                optionId: 1,
                new DateTime(2026, 6, 20, 8, 6, 0),
                CancellationToken.None);

            Assert.True(handled);
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.NotNull(updated);
            var expectedConquerPoints = 12 + TunedLegacyHourlyEventPrizeConquerPoints;
            Assert.Equal(expectedConquerPoints, updated.Character.ConquerPoints);
            Assert.Equal(1_002, updated.Character.MapId);
            Assert.Equal(410, updated.Character.X);
            Assert.Equal(355, updated.Character.Y);

            Assert.Contains(
                ParseMsgUpdates(sent),
                packet =>
                    packet.Uid == 1_293_600U &&
                    packet.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                        entry.Value == checked((ulong)expectedConquerPoints)));
            Assert.DoesNotContain(
                ParseMsgUpdates(sent),
                packet => packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Conquer));

            var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal(expectedConquerPoints, persisted.ConquerPoints);

            var messages = ParseMsgMessages(sent);
            Assert.Contains(
                messages,
                message =>
                    message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                    message.Content.Any(text =>
                        text.Contains("HourlyHero", StringComparison.Ordinal) &&
                        text.Contains("50000 Cps", StringComparison.Ordinal)));
            Assert.DoesNotContain(
                messages,
                message => message.Content.Any(text => text.Contains("500000 Cps", StringComparison.Ordinal)));

            var output = log.ToString();
            Assert.Contains("WorldServer persisted LegacyNpc HourlyEvents prize", output);
            Assert.Contains("path=100.ini keys=ConquerPoints", output);
            Assert.Contains("source=NpcHandler.EventOutxx5 option=1 map=10770 alivePlayers=1", output);
            Assert.Contains($"sourceConquerPoints={LegacyHourlyEventSourcePrizeConquerPoints}", output);
            Assert.Contains($"tunedConquerPoints={TunedLegacyHourlyEventPrizeConquerPoints}", output);
            Assert.Contains("previousConquerPoints=12", output);
            Assert.Contains($"updatedConquerPoints={expectedConquerPoints}", output);
        }
        finally
        {
            DeleteTempRoot(dbRoot);
        }
    }

    [Fact]
    public async Task HandlerSkipsHourlyPrizeWhenAlivePlayerGateIsNotSoleWinner()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 10_770, conquerPoints: 12);
        var rivalSent = new List<byte[]>();
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalSession = CreateSession(
            mapId: 10_770,
            uid: 1_293_601,
            accountId: 101,
            conquerPoints: 40);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        sessions.Set(rivalConnection, rivalSession);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventOutxx5NpcId,
            optionId: 1,
            new DateTime(2026, 6, 20, 8, 6, 0),
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(12, updated.Character.ConquerPoints);
        Assert.Equal(10_770, updated.Character.MapId);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseMsgActions(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc HourlyEvents prize", output);
        Assert.Contains("source=NpcHandler.EventOutxx5 option=1 map=10770 alivePlayers=2", output);
        Assert.Contains("source requires exactly one alive player", output);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x = 50,
        long y = 50,
        string dynamicId = "0",
        long conquerPoints = 0,
        long uid = 1_293_600,
        long accountId = 100,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: "hourly-events-test",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: "HourlyHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = dynamicId,
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMsgMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static string WriteSnapshot(
        string usersRoot,
        long accountId,
        long uid,
        long conquerPoints)
    {
        var path = Path.Combine(usersRoot, accountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=0
            Name=HourlyHero
            Class=7051
            Map=10770
            X=50
            Y=50
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=10770
            PMapX=50
            PMapY=50
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0

            [PaidNobility]
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """);
        return path;
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "wolf-hourly-events-" + Guid.NewGuid());
    }

    private static void DeleteTempRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }
}
