using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildWarJailPardonNpcTests
{
    private const uint GuildWarJailNpcId = 4_026_307;

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(40)]
    [InlineData(45)]
    public async Task PardonOptionOffersReleaseAtEveryInclusiveSourceBoundary(int minute)
    {
        var result = await InvokeAsync(optionId: 2, minute);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "It is time for a Guild War pardon. You may leave the jail now.");
        Assert.Contains(replies, reply => reply.Text == "Leave jail." && reply.OptionId == 3);
        Assert.Contains(replies, reply => reply.Text == "I want to stay." && reply.OptionId == byte.MaxValue);
        Assert.Contains($"minute={minute} open=True", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(19)]
    [InlineData(26)]
    [InlineData(39)]
    [InlineData(46)]
    [InlineData(59)]
    public async Task PardonOptionKeepsPlayerInJailOutsideSourceWindows(int minute)
    {
        var result = await InvokeAsync(optionId: 2, minute);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Rest for a while. Pardons are available at minutes 00-05, 20-25, and 40-45.");
        Assert.Contains(replies, reply => reply.Text == "I understand." && reply.OptionId == byte.MaxValue);
        Assert.DoesNotContain(replies, reply => reply.OptionId == 3);
        Assert.Empty(ParseMsgActions(result.Sent));
        Assert.Equal(1_035, result.Session.Character.MapId);
        Assert.Contains($"minute={minute} open=False", result.Log);
    }

    [Fact]
    public async Task ReleaseOptionRevivesAndTeleportsToSourceTwinCityDestination()
    {
        var result = await InvokeAsync(optionId: 3, minute: 20, hitPoints: 0);

        Assert.True(result.Handled);
        Assert.Equal(500, result.Session.Character.MinHitPoints);
        Assert.Equal(1_002, result.Session.Character.MapId);
        Assert.Equal(410, result.Session.Character.X);
        Assert.Equal(354, result.Session.Character.Y);
        Assert.Equal("0", result.Session.Character.Attributes["DynamicID"]);

        var teleport = Assert.Single(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, teleport.DwParam);
        Assert.Equal(410U, teleport.PositionX);
        Assert.Equal(354U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.GWJail map=1002 x=410 y=354", result.Log);
    }

    private static async Task<InvocationResult> InvokeAsync(byte optionId, int minute, long hitPoints = 500)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var session = CreateSession(hitPoints);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 14, 12, minute, 0, TimeSpan.Zero));

        var handled = await WorldPacketHandlers.TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                TimeProvider: timeProvider),
            session,
            GuildWarJailNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvocationResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(long hitPoints)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "guild-war-jail-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "JailedHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_035,
                X: 30,
                Y: 30,
                PreviousMapId: 1_002,
                PreviousMapX: 410,
                PreviousMapY: 354,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: hitPoints,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "77",
                    ["MaxHitPoints"] = "500",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }

    private sealed record InvocationResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session);
}
