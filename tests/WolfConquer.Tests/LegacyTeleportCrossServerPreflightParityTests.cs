using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportCrossServerPreflightParityTests
{
    private const uint CharacterUid = 1_293_700;
    private const int SourceMapId = 1_015;
    private const int OrdinaryDestinationMapId = 1_016;
    private const int SourceX = 52;
    private const int SourceY = 50;
    private const int PortalX = 50;
    private const int PortalY = 50;
    private const int DestinationX = 60;
    private const int DestinationY = 60;
    private const int PrayingFlag = 0x1F;
    private const int CastPrayFlag = 0x1E;
    private const int MagicDefenderFlag = 0x80;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    public static TheoryData<string, int, string?, string?> ForeignServerAllowances => new()
    {
        { "own server", OrdinaryDestinationMapId, "OnMyOwnServer", "true" },
        { "destination map 1002", 1_002, null, null },
        { "destination map 3935", 3_935, null, null },
        { "destination map 10137", 10_137, null, null },
        { "destination map 8709", 8_709, null, null },
        { "destination map 5339", 5_339, null, null },
        { "qualifier", OrdinaryDestinationMapId, "InQualifier", "true" },
        { "watching", OrdinaryDestinationMapId, "ArenaWatching", "true" },
        { "SetLocationType 1", OrdinaryDestinationMapId, "SetLocationType", "1" },
        { "SetLocationType 2", OrdinaryDestinationMapId, "SetLocationType", "2" },
        { "SetLocationType 22", OrdinaryDestinationMapId, "SetLocationType", "22" },
    };

    [Fact]
    public async Task ForeignServerChangeMapToOrdinaryDestinationIsDeniedBeforeAnyTeleportSideEffects()
    {
        var result = await RunChangeMapAsync(
            OrdinaryDestinationMapId,
            CreateForeignServerAttributes(includeCleanupFlags: true));

        AssertDeniedWithoutSideEffects(result, expectChangeMapAcknowledgement: true);
    }

    [Theory]
    [MemberData(nameof(ForeignServerAllowances))]
    public async Task ChangeMapAllowsEveryMatchingCrossServerException(
        string allowance,
        int destinationMapId,
        string? allowanceAttribute,
        string? allowanceValue)
    {
        var attributes = CreateForeignServerAttributes(includeCleanupFlags: true);
        if (allowanceAttribute is not null)
        {
            attributes[allowanceAttribute] = Assert.IsType<string>(allowanceValue);
        }

        var result = await RunChangeMapAsync(destinationMapId, attributes);

        AssertDestinationTransition(result, destinationMapId, allowance);
    }

    [Fact]
    public async Task ForeignServerProjectManagerDoesNotBypassOwnershipDenial()
    {
        var attributes = CreateForeignServerAttributes(includeCleanupFlags: true);
        attributes["ProjectManager"] = "1";

        var result = await RunChangeMapAsync(OrdinaryDestinationMapId, attributes);

        Assert.Equal("1", result.SourceSession.Character.Attributes["ProjectManager"]);
        AssertDeniedWithoutSideEffects(result, expectChangeMapAcknowledgement: true);
    }

    [Fact]
    public async Task ForeignServerCanTeleportOverrideDoesNotBypassOwnershipDenial()
    {
        var harness = CreateHarness(CreateForeignServerAttributes(includeCleanupFlags: true));
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            harness.Connection,
            log,
            Session: harness.SourceSession,
            Sessions: harness.Sessions);

        // The sole canTeleport:true caller targets allowlisted map 1002, so invoke the same
        // production helper with an ordinary destination to isolate the override's behavior.
        await InvokeNpcTeleportAsync(
            context,
            harness.SourceSession,
            OrdinaryDestinationMapId,
            canTeleport: true);

        AssertDeniedWithoutSideEffects(ReadResult(harness));
    }

    private static async Task<TransitionResult> RunChangeMapAsync(
        int destinationMapId,
        IReadOnlyDictionary<string, string> attributes)
    {
        var harness = CreateHarness(attributes);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            portals:
            [
                new Portal(
                    SourceMapId,
                    PortalX,
                    PortalY,
                    destinationMapId,
                    DestinationX,
                    DestinationY),
            ],
            maps:
            [
                CreateMapMetadata(SourceMapId),
                CreateMapMetadata(destinationMapId),
            ]);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                harness.Connection,
                log,
                Session: harness.SourceSession,
                WorldData: worldData,
                Sessions: harness.Sessions),
            ReadFrame(new MsgActionPacket
            {
                ObjId = CharacterUid,
                Timestamp = 4_321,
                Type = MsgActionType.ChangeMap,
            }.ToFrameBytes()));

        return ReadResult(harness);
    }

    private static async ValueTask InvokeNpcTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        int destinationMapId,
        bool canTeleport)
    {
        var method = Assert.Single(
            typeof(WorldPacketHandlers).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            candidate =>
                candidate.Name == "SendLegacyNpcTeleportAsync" &&
                candidate.GetParameters().Length == 8);
        var destinationType = method.GetParameters()[4].ParameterType;
        var destination = Activator.CreateInstance(
            destinationType,
            (long)destinationMapId,
            (long)DestinationX,
            (long)DestinationY,
            "LegacyTeleportCrossServerPreflightParityTests.CanTeleport");
        Assert.NotNull(destination);

        var invocation = method.Invoke(
            null,
            [
                context,
                session,
                0u,
                (byte)0,
                destination,
                CancellationToken.None,
                canTeleport,
                session,
            ]);

        await Assert.IsType<ValueTask>(invocation);
    }

    private static TeleportHarness CreateHarness(IReadOnlyDictionary<string, string> attributes)
    {
        var clientSent = new List<byte[]>();
        var connection = CreateSendingConnection(clientSent);
        var sourceSession = CreateSession(
            SourceMapId,
            SourceX,
            SourceY,
            attributes,
            CharacterUid);
        var observerSent = new List<byte[]>();
        var observerConnection = CreateSendingConnection(observerSent);
        var observerSession = CreateSession(
            SourceMapId,
            SourceX + 1,
            SourceY,
            CreateOwnServerAttributes(),
            CharacterUid + 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, sourceSession);
        sessions.Set(observerConnection, observerSession);

        return new TeleportHarness(
            connection,
            observerConnection,
            sourceSession,
            sessions,
            clientSent,
            observerSent);
    }

    private static TransitionResult ReadResult(TeleportHarness harness)
    {
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var storedSession));
        return new TransitionResult(
            harness.SourceSession,
            Assert.IsType<WorldSession>(storedSession),
            harness.ClientSent,
            harness.ObserverSent);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string> attributes,
        uint uid)
    {
        return new WorldSession(
            AccountId: uid,
            Username: $"teleport-cross-server-{uid}",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: $"CrossServer{uid}",
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
                X: x,
                Y: y,
                PreviousMapId: 1_000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 500,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "teleport-cross-server-fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static Dictionary<string, string> CreateForeignServerAttributes(
        bool includeCleanupFlags = false)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "77",
            ["MaxHitPoints"] = "500",
            ["FixtureMarker"] = "cross-server-source",
            ["OnMyOwnServer"] = "false",
        };

        if (includeCleanupFlags)
        {
            attributes["StatusFlags"] = BuildStatusFlags(
                PrayingFlag,
                CastPrayFlag,
                MagicDefenderFlag);
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateOwnServerAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "77",
            ["MaxHitPoints"] = "500",
            ["OnMyOwnServer"] = "true",
        };
    }

    private static void AssertDeniedWithoutSideEffects(
        TransitionResult result,
        bool expectChangeMapAcknowledgement = false)
    {
        Assert.Same(result.SourceSession, result.StoredSession);
        Assert.Equal(result.SourceSession.Character.MapId, result.StoredSession.Character.MapId);
        Assert.Equal(result.SourceSession.Character.X, result.StoredSession.Character.X);
        Assert.Equal(result.SourceSession.Character.Y, result.StoredSession.Character.Y);
        Assert.Equal(result.SourceSession.Character.PreviousMapId, result.StoredSession.Character.PreviousMapId);
        Assert.Equal(result.SourceSession.Character.PreviousMapX, result.StoredSession.Character.PreviousMapX);
        Assert.Equal(result.SourceSession.Character.PreviousMapY, result.StoredSession.Character.PreviousMapY);
        AssertAttributesEqual(
            result.SourceSession.Character.Attributes,
            result.StoredSession.Character.Attributes);

        Assert.True(HasStatusFlag(result.StoredSession.Character.Attributes, PrayingFlag));
        Assert.True(HasStatusFlag(result.StoredSession.Character.Attributes, CastPrayFlag));
        Assert.True(HasStatusFlag(result.StoredSession.Character.Attributes, MagicDefenderFlag));
        Assert.Empty(result.ObserverSent);

        if (expectChangeMapAcknowledgement)
        {
            var acknowledgement = Assert.Single(ParseActions(result.ClientSent));
            Assert.Equal(MsgActionType.ChangeMap, acknowledgement.Type);
            Assert.Equal(CharacterUid, acknowledgement.ObjId);
            return;
        }

        Assert.Empty(result.ClientSent);
    }

    private static void AssertDestinationTransition(
        TransitionResult result,
        int destinationMapId,
        string allowance)
    {
        Assert.NotSame(result.SourceSession, result.StoredSession);
        Assert.Equal(destinationMapId, result.StoredSession.Character.MapId);
        Assert.Equal(DestinationX, result.StoredSession.Character.X);
        Assert.Equal(DestinationY, result.StoredSession.Character.Y);
        Assert.Equal(SourceMapId, result.StoredSession.Character.PreviousMapId);
        Assert.Equal(SourceX, result.StoredSession.Character.PreviousMapX);
        Assert.Equal(SourceY, result.StoredSession.Character.PreviousMapY);
        Assert.False(HasStatusFlag(result.StoredSession.Character.Attributes, PrayingFlag));
        Assert.False(HasStatusFlag(result.StoredSession.Character.Attributes, CastPrayFlag));
        Assert.False(HasStatusFlag(result.StoredSession.Character.Attributes, MagicDefenderFlag));

        var teleport = Assert.Single(
            ParseActions(result.ClientSent),
            action => action.Type == MsgActionType.Teleport);
        Assert.Equal((uint)DestinationX, teleport.PositionX);
        Assert.Equal((uint)DestinationY, teleport.PositionY);
        Assert.Contains(result.ObserverSent, IsRemoveEntityFrame);
        Assert.True(
            result.ClientSent.Any(IsTeleportStatusCleanupFrame),
            $"Expected teleport cleanup before the '{allowance}' allowance transition.");
    }

    private static void AssertAttributesEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            Assert.True(actual.TryGetValue(key, out var actualValue), $"Missing source attribute '{key}'.");
            Assert.Equal(value, actualValue);
        }
    }

    private static bool HasStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return LegacyPlayerDeathState.HasStatusFlag(attributes, flag);
    }

    private static bool IsTeleportStatusCleanupFrame(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.StatusFlag ||
                (update.Type == LegacyMsgUpdateDataType.AzureShield &&
                 update.Flag == MagicDefenderFlag));
    }

    private static bool IsRemoveEntityFrame(byte[] bytes)
    {
        return LegacyRemoveEntityPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
    {
        var actions = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                actions.Add(packet);
            }
        }

        return actions;
    }

    private static MapMetadata CreateMapMetadata(int mapId)
    {
        return new MapMetadata(
            mapId,
            mapId.ToString(CultureInfo.InvariantCulture) + ".ini",
            Name: null,
            MapDoc: mapId,
            TypeRaw: "0",
            Type: 0,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: mapId,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>());
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

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record TeleportHarness(
        ConquerTcpConnection Connection,
        ConquerTcpConnection ObserverConnection,
        WorldSession SourceSession,
        WorldSessionStore Sessions,
        List<byte[]> ClientSent,
        List<byte[]> ObserverSent);

    private sealed record TransitionResult(
        WorldSession SourceSession,
        WorldSession StoredSession,
        IReadOnlyList<byte[]> ClientSent,
        IReadOnlyList<byte[]> ObserverSent);
}
