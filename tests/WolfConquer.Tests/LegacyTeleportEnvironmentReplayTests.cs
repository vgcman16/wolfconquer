using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportEnvironmentReplayTests
{
    private const int FatalStrikeFlag = 0x2F;
    private const long CharacterUid = 1_293_600;
    private const long PeerUid = 1_293_601;
    private const long DestinationMapId = 3_868;
    private const long DestinationX = 32;
    private const long DestinationY = 73;
    private const uint TeleportTimestamp = 4_321;
    private const uint EnvironmentColor = 0x00AB_CDEF;

    [Theory]
    [InlineData(LegacyWeatherType.Nothing, "color", "weatherType")]
    [InlineData(LegacyWeatherType.Rain, "Color", "WeatherType")]
    [InlineData(LegacyWeatherType.Snow, "color", "WeatherType")]
    [InlineData(LegacyWeatherType.RainWind, "Color", "weatherType")]
    [InlineData(LegacyWeatherType.CherryBlossomPetals, "color", "weatherType")]
    [InlineData(LegacyWeatherType.CherryBlossomPetalsWind, "Color", "WeatherType")]
    [InlineData(LegacyWeatherType.BlowingCotten, "color", "WeatherType")]
    [InlineData(LegacyWeatherType.Atoms, "Color", "weatherType")]
    public async Task SharedExecutorReplaysColorThenSupportedWeatherAfterVisibilityAndCleanup(
        LegacyWeatherType weatherType,
        string colorKey,
        string weatherKey)
    {
        var harness = CreateHarness(CreateSession(
            colorKey,
            EnvironmentColor.ToString(CultureInfo.InvariantCulture),
            weatherKey,
            ((uint)weatherType).ToString(CultureInfo.InvariantCulture)));

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            harness.Context,
            harness.Source,
            new LegacyTeleportDestination(DestinationMapId, DestinationX, DestinationY),
            TeleportTimestamp,
            CancellationToken.None);
        GC.KeepAlive(harness.PeerConnection);

        var updated = Assert.IsType<WorldSession>(result);
        AssertDestinationState(harness, updated);

        var frames = DecodeFrames(harness.Sent);
        var actions = ParseActions(frames);
        var teleport = Assert.Single(actions, item => item.Packet.Type == MsgActionType.Teleport);
        Assert.Equal((uint)CharacterUid, teleport.Packet.ObjId);
        Assert.Equal(TeleportTimestamp, teleport.Packet.Timestamp);
        Assert.Equal((uint)DestinationMapId, teleport.Packet.DwParam);
        Assert.Equal((uint)DestinationMapId, teleport.Packet.DwParam3);
        Assert.Equal((uint)DestinationX, teleport.Packet.PositionX);
        Assert.Equal((uint)DestinationY, teleport.Packet.PositionY);

        // The shared transition already sends map color before map status. The
        // matching-decompile environment replay is the second color, after cleanup.
        var colors = actions.Where(item => item.Packet.Type == MsgActionType.LegacyColor).ToArray();
        Assert.Equal(2, colors.Length);
        var mapColor = Assert.Single(colors, item => item.Packet.DwParam == 0);
        var environmentColor = Assert.Single(colors, item => item.Packet.DwParam == EnvironmentColor);
        Assert.Equal((uint)CharacterUid, environmentColor.Packet.ObjId);
        Assert.Equal((uint)DestinationX, environmentColor.Packet.PositionX);
        Assert.Equal((uint)DestinationY, environmentColor.Packet.PositionY);

        var mapStatus = Assert.Single(ParseMapStatuses(frames));
        Assert.Equal(
            new LegacyMapStatusPacket(
                (uint)DestinationMapId,
                (uint)DestinationMapId,
                Status: 0),
            mapStatus.Packet);
        var visibility = Assert.Single(
            frames,
            item => item.Frame.Header.Opcode == LegacyPlayerSpawnPacket.Opcode);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(visibility.Frame, out _));
        var cleanup = Assert.Single(ParseFatalStrikeCleanupUpdates(frames));
        Assert.Equal((uint)CharacterUid, cleanup.Packet.Uid);

        var weather = Assert.Single(ParseWeather(frames));
        Assert.Equal(weatherType, weather.Packet.Type);
        Assert.Equal(250U, weather.Packet.Intensity);
        Assert.Equal(250U, weather.Packet.Direction);
        Assert.Equal(250U, weather.Packet.Color);
        Assert.Equal(250U, weather.Packet.SpeedSeconds);

        Assert.True(teleport.Index < mapColor.Index);
        Assert.True(mapColor.Index < mapStatus.Index);
        Assert.True(mapStatus.Index < visibility.Index);
        Assert.True(visibility.Index < cleanup.Index);
        Assert.True(cleanup.Index < environmentColor.Index);
        Assert.Equal(environmentColor.Index + 1, weather.Index);
        Assert.Equal(frames.Length - 1, weather.Index);
    }

    [Theory]
    [InlineData("color", "weatherType", "0")]
    [InlineData("Color", "WeatherType", "6")]
    public async Task SharedExecutorSendsNoEnvironmentReplayForZeroColorAndSilentWeather(
        string colorKey,
        string weatherKey,
        string weatherValue)
    {
        var harness = CreateHarness(CreateSession(colorKey, "0", weatherKey, weatherValue));

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            harness.Context,
            harness.Source,
            new LegacyTeleportDestination(DestinationMapId, DestinationX, DestinationY),
            TeleportTimestamp,
            CancellationToken.None);
        GC.KeepAlive(harness.PeerConnection);

        var updated = Assert.IsType<WorldSession>(result);
        AssertDestinationState(harness, updated);

        var frames = DecodeFrames(harness.Sent);
        var actions = ParseActions(frames);
        var teleport = Assert.Single(actions, item => item.Packet.Type == MsgActionType.Teleport);
        Assert.Equal((uint)CharacterUid, teleport.Packet.ObjId);
        Assert.Equal((uint)DestinationMapId, teleport.Packet.DwParam);
        Assert.Equal((uint)DestinationX, teleport.Packet.PositionX);
        Assert.Equal((uint)DestinationY, teleport.Packet.PositionY);

        var mapColor = Assert.Single(
            actions,
            item => item.Packet.Type == MsgActionType.LegacyColor);
        Assert.Equal(0U, mapColor.Packet.DwParam);
        Assert.Empty(ParseWeather(frames));

        var mapStatus = Assert.Single(ParseMapStatuses(frames));
        var visibility = Assert.Single(
            frames,
            item => item.Frame.Header.Opcode == LegacyPlayerSpawnPacket.Opcode);
        var cleanup = Assert.Single(ParseFatalStrikeCleanupUpdates(frames));

        Assert.True(teleport.Index < mapColor.Index);
        Assert.True(mapColor.Index < mapStatus.Index);
        Assert.True(mapStatus.Index < visibility.Index);
        Assert.True(visibility.Index < cleanup.Index);
        Assert.Equal(frames.Length - 1, cleanup.Index);
    }

    private static void AssertDestinationState(EnvironmentHarness harness, WorldSession updated)
    {
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var stored));
        Assert.Same(updated, stored);
        Assert.Equal(CharacterUid, updated.Character.Uid);
        Assert.Equal(DestinationMapId, updated.Character.MapId);
        Assert.Equal(DestinationX, updated.Character.X);
        Assert.Equal(DestinationY, updated.Character.Y);
        Assert.False(HasStatusFlag(updated.Character, FatalStrikeFlag));
        Assert.False(updated.Character.Attributes.ContainsKey("LegacyFatalStrikeExpiresAtUtc"));
    }

    private static EnvironmentHarness CreateHarness(WorldSession source)
    {
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var peerConnection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: static (_, _) => ValueTask.CompletedTask);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, source);
        sessions.Set(peerConnection, CreatePeerSession(source));
        var context = new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            Session: source,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        return new EnvironmentHarness(connection, peerConnection, source, sessions, context, sent);
    }

    private static WorldSession CreateSession(
        string colorKey,
        string colorValue,
        string weatherKey,
        string weatherValue)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StatusFlags"] = BuildStatusFlags(FatalStrikeFlag),
            ["LegacyFatalStrikeExpiresAtUtc"] = DateTimeOffset.UtcNow
                .AddMinutes(1)
                .ToString("O", CultureInfo.InvariantCulture),
            [colorKey] = colorValue,
            [weatherKey] = weatherValue,
        };

        return new WorldSession(
            AccountId: 2,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "HeroOne",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 410,
                PreviousMapY: 354,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 789,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: string.Empty,
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static WorldSession CreatePeerSession(WorldSession source)
    {
        var peerAttributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StatusFlags"] = BuildStatusFlags(),
        };
        return source with
        {
            AccountId = 3,
            Username = "VisiblePeer",
            Character = source.Character with
            {
                Uid = PeerUid,
                Name = "VisiblePeer",
                MapId = DestinationMapId,
                X = DestinationX + 1,
                Y = DestinationY,
                Attributes = peerAttributes,
            },
        };
    }

    private static IndexedFrame[] DecodeFrames(IReadOnlyList<byte[]> sent)
    {
        var frames = new IndexedFrame[sent.Count];
        for (var index = 0; index < sent.Count; index++)
        {
            Assert.True(PacketFrameDecoder.TryRead(
                new ReadOnlySequence<byte>(sent[index]),
                out var frame,
                out _,
                out _));
            frames[index] = new IndexedFrame(index, frame);
        }

        return frames;
    }

    private static IndexedAction[] ParseActions(IEnumerable<IndexedFrame> frames)
    {
        var actions = new List<IndexedAction>();
        foreach (var frame in frames)
        {
            if (MsgActionPacket.TryParse(frame.Frame, out var packet))
            {
                actions.Add(new IndexedAction(frame.Index, packet));
            }
        }

        return actions.ToArray();
    }

    private static IndexedMapStatus[] ParseMapStatuses(IEnumerable<IndexedFrame> frames)
    {
        var packets = new List<IndexedMapStatus>();
        foreach (var frame in frames)
        {
            if (LegacyMapStatusPacket.TryParse(frame.Frame, out var packet))
            {
                packets.Add(new IndexedMapStatus(frame.Index, packet));
            }
        }

        return packets.ToArray();
    }

    private static IndexedUpdate[] ParseFatalStrikeCleanupUpdates(IEnumerable<IndexedFrame> frames)
    {
        var packets = new List<IndexedUpdate>();
        foreach (var frame in frames)
        {
            if (!LegacyMsgUpdatePacket.TryParse(frame.Frame, out var packet) ||
                !packet.Updates.Any(update =>
                    update.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    update.Values is not null &&
                    !IsStatusFlagSet(update.Values, FatalStrikeFlag)))
            {
                continue;
            }

            packets.Add(new IndexedUpdate(frame.Index, packet));
        }

        return packets.ToArray();
    }

    private static IndexedWeather[] ParseWeather(IEnumerable<IndexedFrame> frames)
    {
        var packets = new List<IndexedWeather>();
        foreach (var frame in frames)
        {
            if (LegacyWeatherPacket.TryParse(frame.Frame, out var packet))
            {
                packets.Add(new IndexedWeather(frame.Index, packet));
            }
        }

        return packets.ToArray();
    }

    private static bool HasStatusFlag(CharacterSnapshot character, int flag)
    {
        var values = character.Attributes["StatusFlags"]
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => ulong.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        return IsStatusFlagSet(values, flag);
    }

    private static bool IsStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private sealed record EnvironmentHarness(
        ConquerTcpConnection Connection,
        ConquerTcpConnection PeerConnection,
        WorldSession Source,
        WorldSessionStore Sessions,
        WorldPacketHandlerContext Context,
        List<byte[]> Sent);

    private sealed record IndexedFrame(int Index, PacketFrame Frame);

    private sealed record IndexedAction(int Index, MsgActionPacket Packet);

    private sealed record IndexedMapStatus(int Index, LegacyMapStatusPacket Packet);

    private sealed record IndexedUpdate(int Index, LegacyMsgUpdatePacket Packet);

    private sealed record IndexedWeather(int Index, LegacyWeatherPacket Packet);
}
