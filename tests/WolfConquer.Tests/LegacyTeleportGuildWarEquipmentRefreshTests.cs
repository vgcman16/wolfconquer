using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportGuildWarEquipmentRefreshTests
{
    private const long GuildWarMapId = 1_038;
    private const long OtherSourceMapId = 1_002;
    private const long OtherDestinationMapId = 3_868;
    private const long DestinationX = 32;
    private const long DestinationY = 73;
    private const uint EnvironmentColor = 0x00AB_CDEF;

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 2)]
    public async Task SharedExecutorRefreshesEquipmentForEachIndependentGuildWarMapCheck(
        bool destinationIsGuildWar,
        bool previousIsGuildWar,
        int expectedRefreshCount)
    {
        var sourceMapId = previousIsGuildWar ? GuildWarMapId : OtherSourceMapId;
        var destinationMapId = destinationIsGuildWar ? GuildWarMapId : OtherDestinationMapId;
        var harness = CreateHarness(CreateSession(
            sourceMapId,
            previousMapId: sourceMapId,
            PlayerItemSnapshot.Empty,
            alternateEquipmentActive: false));

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            harness.Context,
            harness.Source,
            new LegacyTeleportDestination(destinationMapId, DestinationX, DestinationY),
            timestamp: 4_321,
            CancellationToken.None);

        var updated = Assert.IsType<WorldSession>(result);
        Assert.Equal(destinationMapId, updated.Character.MapId);
        Assert.Equal(sourceMapId, updated.Character.PreviousMapId);
        Assert.Equal(
            expectedRefreshCount,
            ParseEquipmentRefreshes(DecodeFrames(harness.Sent)).Length);
    }

    [Fact]
    public async Task SharedExecutorSendsExactAlternateEquipmentAfterColorThenWeather()
    {
        var equipment = CreateEquipment();
        var harness = CreateHarness(CreateSession(
            OtherSourceMapId,
            previousMapId: OtherSourceMapId,
            equipment,
            alternateEquipmentActive: true));

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            harness.Context,
            harness.Source,
            new LegacyTeleportDestination(GuildWarMapId, DestinationX, DestinationY),
            timestamp: 4_321,
            CancellationToken.None);

        Assert.IsType<WorldSession>(result);
        var frames = DecodeFrames(harness.Sent);
        var environmentColor = Assert.Single(
            ParseActions(frames),
            item => item.Packet.Type == MsgActionType.LegacyColor &&
                item.Packet.DwParam == EnvironmentColor);
        var weather = Assert.Single(ParseWeather(frames));
        var refresh = Assert.Single(ParseEquipmentRefreshes(frames));

        Assert.Equal(environmentColor.Index + 1, weather.Index);
        Assert.Equal(weather.Index + 1, refresh.Index);
        Assert.Equal((ushort)46, refresh.Packet.Action);
        Assert.Equal(1U, refresh.Packet.Alternante);
        Assert.Equal(0U, refresh.Packet.WParam);
        Assert.Equal(
            new LegacyShowEquipmentSlots(
                Head: 51_001,
                Necklace: 52_001,
                Armor: 53_001,
                RightWeapon: 54_001,
                LeftWeapon: 55_001,
                Ring: 56_001,
                Bottle: 57_001,
                Boots: 58_001,
                Garment: 59_001,
                RightWeaponAccessory: 15_001,
                LeftWeaponAccessory: 16_001,
                SteedMount: 17_001,
                RidingCrop: 18_001,
                Wing: 19_001),
            refresh.Packet.EquipmentSlots);
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                equipment,
                alternateEquipmentActive: true),
            refresh.Packet);
    }

    private static TeleportHarness CreateHarness(WorldSession source)
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
        var sessions = new WorldSessionStore();
        sessions.Set(connection, source);
        var context = new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            Session: source,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        return new TeleportHarness(source, context, sent);
    }

    private static WorldSession CreateSession(
        long mapId,
        long previousMapId,
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["PDinamycID"] = "0",
            ["StatusFlags"] = "0/0/0/0/0/0/0/0/0/0/",
            ["color"] = EnvironmentColor.ToString(CultureInfo.InvariantCulture),
            ["weatherType"] = ((uint)LegacyWeatherType.Rain).ToString(CultureInfo.InvariantCulture),
        };
        return new WorldSession(
            AccountId: 2,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
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
                MapId: mapId,
                X: 430,
                Y: 378,
                PreviousMapId: previousMapId,
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
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            AlternateEquipmentActive: alternateEquipmentActive);
    }

    private static PlayerItemSnapshot CreateEquipment()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(51_001, 51),
                Equipment(52_001, 52),
                Equipment(53_001, 53),
                Equipment(54_001, 54),
                Equipment(55_001, 55),
                Equipment(56_001, 56),
                Equipment(57_001, 57),
                Equipment(58_001, 58),
                Equipment(59_001, 59),
                Equipment(15_001, 15),
                Equipment(16_001, 16),
                Equipment(17_001, 17),
                Equipment(18_001, 18),
                Equipment(19_001, 19),
            ],
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem Equipment(uint uid, ushort position)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            Position = position,
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
        return frames
            .Where(item => MsgActionPacket.TryParse(item.Frame, out _))
            .Select(item =>
            {
                Assert.True(MsgActionPacket.TryParse(item.Frame, out var packet));
                return new IndexedAction(item.Index, packet);
            })
            .ToArray();
    }

    private static IndexedWeather[] ParseWeather(IEnumerable<IndexedFrame> frames)
    {
        return frames
            .Where(item => LegacyWeatherPacket.TryParse(item.Frame, out _))
            .Select(item =>
            {
                Assert.True(LegacyWeatherPacket.TryParse(item.Frame, out var packet));
                return new IndexedWeather(item.Index, packet);
            })
            .ToArray();
    }

    private static IndexedEquipment[] ParseEquipmentRefreshes(IEnumerable<IndexedFrame> frames)
    {
        return frames
            .Where(item => LegacyShowEquipmentPacket.TryParse(item.Frame, out var packet) &&
                packet.Action == LegacyShowEquipmentPacket.ShowAction)
            .Select(item =>
            {
                Assert.True(LegacyShowEquipmentPacket.TryParse(item.Frame, out var packet));
                return new IndexedEquipment(item.Index, packet);
            })
            .ToArray();
    }

    private sealed record TeleportHarness(
        WorldSession Source,
        WorldPacketHandlerContext Context,
        List<byte[]> Sent);

    private sealed record IndexedFrame(int Index, PacketFrame Frame);

    private sealed record IndexedAction(int Index, MsgActionPacket Packet);

    private sealed record IndexedWeather(int Index, LegacyWeatherPacket Packet);

    private sealed record IndexedEquipment(int Index, LegacyShowEquipmentPacket Packet);
}
