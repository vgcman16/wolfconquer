using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportDestinationStatusCleanupTests
{
    private const int FatalStrikeFlag = 0x2F;
    private const int UnrelatedStatusFlag = 0x17;
    private const string FatalStrikeExpiresAtAttribute = "LegacyFatalStrikeExpiresAtUtc";

    [Fact]
    public async Task Map1038ClearsFatalStrikeAndPublishesStatusVector()
    {
        await AssertSuccessfulCleanupAsync(1_038);
    }

    [Fact]
    public async Task Map3868ClearsFatalStrikeAndPublishesStatusVector()
    {
        await AssertSuccessfulCleanupAsync(3_868);
    }

    [Fact]
    public async Task DestinationExecutorClearsFatalStrikeAfterTeleportPacket()
    {
        var harness = CreateHarness(CreateSession(mapId: 1_002, includeFatalStrike: true));

        var result = await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(
            harness.Context,
            harness.Source,
            new LegacyTeleportDestination(1_038, 32, 73),
            timestamp: 4_321,
            CancellationToken.None);

        var updated = Assert.IsType<WorldSession>(result);
        AssertStoredSession(harness, updated);
        Assert.Equal(1_038, updated.Character.MapId);
        Assert.False(HasStatusFlag(updated.Character, FatalStrikeFlag));
        Assert.False(updated.Character.Attributes.ContainsKey(FatalStrikeExpiresAtAttribute));

        var teleportIndex = harness.Sent.FindIndex(bytes =>
            TryParseAction(bytes)?.Type == MsgActionType.Teleport);
        var statusIndex = harness.Sent.FindIndex(bytes =>
            TryParseUpdate(bytes)?.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.StatusFlag &&
                update.Values is not null &&
                !IsStatusFlagSet(update.Values, FatalStrikeFlag)) == true);
        Assert.True(teleportIndex >= 0);
        Assert.True(statusIndex > teleportIndex);
    }

    [Fact]
    public async Task UnrelatedMapPreservesFatalStrikeWithoutPublishing()
    {
        var harness = CreateHarness(CreateSession(mapId: 1_002, includeFatalStrike: true));
        var publicationsBeforeCleanup = harness.PublicationCount();

        var result = await WorldPacketHandlers.ApplyLegacyTeleportDestinationStatusCleanupAsync(
            harness.Context,
            harness.Source,
            CancellationToken.None);

        var unchanged = Assert.IsType<WorldSession>(result);
        Assert.Same(harness.Source, unchanged);
        AssertStoredSession(harness, harness.Source);
        Assert.Equal(publicationsBeforeCleanup, harness.PublicationCount());
        Assert.True(HasStatusFlag(unchanged.Character, FatalStrikeFlag));
        Assert.True(unchanged.Character.Attributes.ContainsKey(FatalStrikeExpiresAtAttribute));
        Assert.Empty(harness.Sent);
    }

    [Fact]
    public async Task TargetMapWithoutFatalStrikePreservesAllStateWithoutPublishing()
    {
        var source = CreateSession(mapId: 1_038, includeFatalStrike: false);
        var attributes = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [FatalStrikeExpiresAtAttribute] = "orphaned-but-preserved",
        };
        source = source with
        {
            Character = source.Character with { Attributes = attributes },
        };
        var harness = CreateHarness(source);
        var publicationsBeforeCleanup = harness.PublicationCount();

        var result = await WorldPacketHandlers.ApplyLegacyTeleportDestinationStatusCleanupAsync(
            harness.Context,
            source,
            CancellationToken.None);

        var unchanged = Assert.IsType<WorldSession>(result);
        Assert.Same(source, unchanged);
        AssertStoredSession(harness, source);
        Assert.Equal(publicationsBeforeCleanup, harness.PublicationCount());
        Assert.Equal("orphaned-but-preserved", unchanged.Character.Attributes[FatalStrikeExpiresAtAttribute]);
        Assert.Empty(harness.Sent);
    }

    [Fact]
    public async Task StaleSessionRejectionFailsClosedWithoutMutationOrPacket()
    {
        var harness = CreateHarness(CreateSession(mapId: 1_038, includeFatalStrike: true));
        var current = harness.Source with
        {
            Character = harness.Source.Character with { Money = 123_456 },
        };
        harness.Sessions.Set(harness.Connection, current);
        var publicationsBeforeCleanup = harness.PublicationCount();

        var result = await WorldPacketHandlers.ApplyLegacyTeleportDestinationStatusCleanupAsync(
            harness.Context,
            harness.Source,
            CancellationToken.None);

        Assert.Null(result);
        AssertStoredSession(harness, current);
        Assert.Equal(publicationsBeforeCleanup, harness.PublicationCount());
        Assert.Equal(123_456, current.Character.Money);
        Assert.True(HasStatusFlag(current.Character, FatalStrikeFlag));
        Assert.True(current.Character.Attributes.ContainsKey(FatalStrikeExpiresAtAttribute));
        Assert.True(HasStatusFlag(harness.Source.Character, FatalStrikeFlag));
        Assert.True(harness.Source.Character.Attributes.ContainsKey(FatalStrikeExpiresAtAttribute));
        Assert.Empty(harness.Sent);
    }

    private static async Task AssertSuccessfulCleanupAsync(long mapId)
    {
        var harness = CreateHarness(CreateSession(mapId, includeFatalStrike: true));
        var publicationsBeforeCleanup = harness.PublicationCount();

        var result = await WorldPacketHandlers.ApplyLegacyTeleportDestinationStatusCleanupAsync(
            harness.Context,
            harness.Source,
            CancellationToken.None);

        var updated = Assert.IsType<WorldSession>(result);
        Assert.NotSame(harness.Source, updated);
        AssertStoredSession(harness, updated);
        Assert.Equal(publicationsBeforeCleanup + 1, harness.PublicationCount());
        Assert.Equal(harness.Source with { Character = updated.Character }, updated);
        Assert.Equal(harness.Source.Character with { Attributes = updated.Character.Attributes }, updated.Character);
        Assert.False(HasStatusFlag(updated.Character, FatalStrikeFlag));
        Assert.True(HasStatusFlag(updated.Character, UnrelatedStatusFlag));
        Assert.False(updated.Character.Attributes.ContainsKey(FatalStrikeExpiresAtAttribute));
        Assert.Equal("preserved", updated.Character.Attributes["FixtureMarker"]);

        var packet = Assert.Single(
            harness.Sent.Select(TryParseUpdate).Where(update => update is not null).Select(update => update!));
        Assert.Equal((uint)updated.Character.Uid, packet.Uid);
        var status = Assert.Single(
            packet.Updates,
            update => update.Type == LegacyMsgUpdateDataType.StatusFlag && update.Values is not null);
        Assert.False(IsStatusFlagSet(status.Values!, FatalStrikeFlag));
        Assert.True(IsStatusFlagSet(status.Values!, UnrelatedStatusFlag));
    }

    private static CleanupHarness CreateHarness(WorldSession source)
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
        var publicationCount = 0;
        var sessions = new WorldSessionStore((_, _, _) => publicationCount++);
        sessions.Set(connection, source);
        var context = new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            Session: source,
            Sessions: sessions);
        return new CleanupHarness(
            connection,
            source,
            sessions,
            context,
            sent,
            () => publicationCount);
    }

    private static WorldSession CreateSession(long mapId, bool includeFatalStrike)
    {
        var flags = includeFatalStrike
            ? BuildStatusFlags(FatalStrikeFlag, UnrelatedStatusFlag)
            : BuildStatusFlags(UnrelatedStatusFlag);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = flags,
            ["FixtureMarker"] = "preserved",
        };
        if (includeFatalStrike)
        {
            attributes[FatalStrikeExpiresAtAttribute] =
                DateTimeOffset.UtcNow.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture);
        }

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

    private static LegacyMsgUpdatePacket? TryParseUpdate(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return LegacyMsgUpdatePacket.TryParse(frame, out var packet) ? packet : null;
    }

    private static MsgActionPacket? TryParseAction(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return MsgActionPacket.TryParse(frame, out var packet) ? packet : null;
    }

    private static void AssertStoredSession(CleanupHarness harness, WorldSession expected)
    {
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var stored));
        Assert.Same(expected, stored);
    }

    private static bool HasStatusFlag(CharacterSnapshot character, int flag)
    {
        return IsStatusFlagSet(ParseStatusFlags(character.Attributes["StatusFlags"]), flag);
    }

    private static bool IsStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static ulong[] ParseStatusFlags(string raw)
    {
        return raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => ulong.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
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

    private sealed record CleanupHarness(
        ConquerTcpConnection Connection,
        WorldSession Source,
        WorldSessionStore Sessions,
        WorldPacketHandlerContext Context,
        List<byte[]> Sent,
        Func<int> PublicationCount);
}
