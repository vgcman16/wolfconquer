using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVisualSettingsNpcTests
{
    private const uint WeatherNpcId = 45_465;
    private const uint HideEffectNpcId = 747_462;

    [Fact]
    public async Task WeatherRootAndSubmenusAreTranslatedSourceShapedAsciiDialogs()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            WeatherNpcId,
            optionId: 0,
            CancellationToken.None));
        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            WeatherNpcId,
            optionId: 33,
            CancellationToken.None));
        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            WeatherNpcId,
            optionId: 34,
            CancellationToken.None));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("change the weather", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.Text == "Weather" && reply.OptionId == 33);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.Text == "Color" && reply.OptionId == 34);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.Text == "Rain and wind" && reply.OptionId == 3);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option && reply.Text == "White" && reply.OptionId == 12);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.Weather", log.ToString());
    }

    [Fact]
    public async Task WeatherColorOptionSendsLegacyColorActionAndStoresTransientAttribute()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(x: 440, y: 390);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WeatherNpcId,
            optionId: 10,
            CancellationToken.None);

        Assert.True(handled);
        var action = Assert.Single(ParseActions(sent));
        Assert.Equal(MsgActionType.LegacyColor, action.Type);
        Assert.Equal(16_755_370U, action.DwParam);
        Assert.Equal(1_293_600U, action.ObjId);
        Assert.Equal(440U, action.PositionX);
        Assert.Equal(390U, action.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("16755370", updated.Character.Attributes["color"]);
        Assert.Contains("option=10 color=16755370", log.ToString());
    }

    [Fact]
    public async Task WeatherOptionSendsWeatherPacketWithoutGapLog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WeatherNpcId,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        var weather = Assert.Single(ParseWeather(sent));
        Assert.Equal(LegacyWeatherType.RainWind, weather.Type);
        Assert.Equal(250U, weather.Intensity);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("4", updated.Character.Attributes["weatherType"]);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc Weather", log.ToString());
    }

    [Fact]
    public async Task HideEffectRootAndToggleUseTransientAttributesAndAsciiText()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            HideEffectNpcId,
            optionId: 0,
            CancellationToken.None));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Hide ground item effects." && reply.OptionId == 33);

        sent.Clear();
        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            HideEffectNpcId,
            optionId: 33,
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("1", updated.Character.Attributes["LegacySpawnEffectHidden"]);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You have hidden ground item effects.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("attribute=LegacySpawnEffectHidden hidden=True", log.ToString());

        sent.Clear();
        Assert.True(await WorldPacketHandlers.TryHandleLegacyVisualSettingsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: updated, Sessions: sessions),
            updated,
            HideEffectNpcId,
            optionId: 0,
            CancellationToken.None));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Show ground item effects." && reply.OptionId == 33);
    }

    private static WorldSession CreateSession(int x = 430, int y = 380)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "visual-settings-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "VisualTest",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 5_051,
                FirstClass: 5_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: x,
                Y: y,
                PreviousMapId: 1_002,
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

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<LegacyWeatherPacket> ParseWeather(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyWeatherPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyWeatherPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static bool IsAscii(string value)
    {
        return value.All(character => character <= 127);
    }
}
