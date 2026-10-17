using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRideExpiryTests
{
    private const int RideStatusFlag = 0x32;
    private const long RiderUid = 1_293_600;
    private static readonly DateTimeOffset TestNow =
        DateTimeOffset.Parse("2026-07-10T22:00:00.0000000+00:00", CultureInfo.InvariantCulture);

    [Fact]
    public void FutureRideExpiryLeavesFlagAndMetadataIntact()
    {
        var session = CreateSession(
            RiderUid,
            "Rider",
            1_002,
            430,
            380,
            CreateRideAttributes(TestNow.AddSeconds(1), includeRideFlag: true));

        Assert.False(WorldServerFrameHandler.TryExpireLegacyRideStatus(
            session.Character,
            TestNow,
            out var unchangedCharacter,
            out var removedVisibleFlag));

        Assert.Same(session.Character, unchangedCharacter);
        Assert.False(removedVisibleFlag);
        Assert.True(HasLegacyStatusFlag(unchangedCharacter.Attributes, RideStatusFlag));
        Assert.Equal("true", unchangedCharacter.Attributes["LegacyRideRemoveOnDead"]);
        Assert.Equal("1", unchangedCharacter.Attributes["LegacyRideStampSeconds"]);
    }

    [Fact]
    public async Task DueMetadataWithoutRideFlagIsClearedWithoutStatusBroadcast()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent, 9_958);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var session = CreateSession(
            RiderUid,
            "Unmounted",
            1_002,
            430,
            380,
            CreateRideAttributes(TestNow.AddSeconds(-1), includeRideFlag: false));
        GetHandlerSessions(handler).Set(connection, session);

        await InvokeRideExpirationsAsync(handler, TestNow);

        var updatedSession = AssertHandlerSession(handler, connection);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, RideStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRideRemoveOnDead"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRideStampSeconds"));
        Assert.Equal("preserved", updatedSession.Character.Attributes["Unrelated"]);
        Assert.Empty(sent);
        Assert.DoesNotContain("WorldServer expired legacy Ride flags", log.ToString());
    }

    [Fact]
    public async Task DueRideFlagIsClearedAndBroadcastToSelfAndVisibleViewer()
    {
        using var log = new StringWriter();
        var riderFrames = new List<byte[]>();
        var visibleFrames = new List<byte[]>();
        var hiddenFrames = new List<byte[]>();
        var riderConnection = CreateSendingConnection(riderFrames, 9_958);
        var visibleConnection = CreateSendingConnection(visibleFrames, 9_959);
        var hiddenConnection = CreateSendingConnection(hiddenFrames, 9_960);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var rider = CreateSession(
            RiderUid,
            "Rider",
            1_002,
            430,
            380,
            CreateRideAttributes(TestNow, includeRideFlag: true));
        var visibleViewer = CreateSession(
            RiderUid + 1,
            "Visible",
            1_002,
            435,
            383,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var hiddenViewer = CreateSession(
            RiderUid + 2,
            "Hidden",
            1_002,
            900,
            900,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var sessions = GetHandlerSessions(handler);
        sessions.Set(riderConnection, rider);
        sessions.Set(visibleConnection, visibleViewer);
        sessions.Set(hiddenConnection, hiddenViewer);

        await InvokeRideExpirationsAsync(handler, TestNow);

        var updatedRider = AssertHandlerSession(handler, riderConnection);
        Assert.False(HasLegacyStatusFlag(updatedRider.Character.Attributes, RideStatusFlag));
        Assert.False(updatedRider.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
        Assert.False(updatedRider.Character.Attributes.ContainsKey("LegacyRideRemoveOnDead"));
        Assert.False(updatedRider.Character.Attributes.ContainsKey("LegacyRideStampSeconds"));

        AssertRideClearedStatusFrame(Assert.Single(riderFrames));
        AssertRideClearedStatusFrame(Assert.Single(visibleFrames));
        Assert.Empty(hiddenFrames);
        Assert.Contains(
            "WorldServer expired legacy Ride flags playerCount=1 viewerCount=2",
            log.ToString());
    }

    private static IReadOnlyDictionary<string, string> CreateRideAttributes(
        DateTimeOffset expiresAt,
        bool includeRideFlag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = includeRideFlag
                ? BuildStatusFlags(RideStatusFlag)
                : BuildStatusFlags(),
            ["LegacyRideExpiresAtUtc"] = expiresAt.ToString("O", CultureInfo.InvariantCulture),
            ["LegacyRideRemoveOnDead"] = "true",
            ["LegacyRideStampSeconds"] = "1",
            ["Unrelated"] = "preserved",
        };
        return attributes;
    }

    private static WorldSession CreateSession(
        long uid,
        string name,
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string> attributes)
    {
        var character = new CharacterSnapshot(
            Uid: uid,
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
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://legacy-ride-expiry",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());

        return new WorldSession(
            AccountId: uid,
            Username: name,
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, port),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static async ValueTask InvokeRideExpirationsAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyRideExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask>(method.Invoke(
            handler,
            [now, CancellationToken.None]));
        await invocation;
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        Assert.True(GetHandlerSessions(handler).TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static void AssertRideClearedStatusFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var update));
        Assert.Equal(checked((uint)RiderUid), update.Uid);
        var status = Assert.Single(
            update.Updates,
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(status.Values);
        Assert.False(IsLegacyStatusFlagSet(status.Values, RideStatusFlag));
    }

    private static bool HasLegacyStatusFlag(
        IReadOnlyDictionary<string, string> attributes,
        int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(
                        part,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var value)
                            ? value
                            : 0)
                    .ToArray(),
                flag);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var maxWordIndex = flags.Length == 0 ? 0 : flags.Max(flag => flag / 32);
        var words = new ulong[Math.Max(10, maxWordIndex + 1)];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }
}
