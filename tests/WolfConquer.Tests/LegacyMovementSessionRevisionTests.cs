using System.Buffers;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMovementSessionRevisionTests
{
    private const int FocusedStatusFlag = 0x15;
    private const uint FocusClientSpellId = 9_000;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task StaleMovementContextPreservesNewerSessionAndSkipsMovementBroadcast()
    {
        var sent = new ConcurrentQueue<byte[]>();
        var callbacks = new ConcurrentQueue<(WorldSession? Previous, WorldSession Current)>();
        var connection = new ConquerTcpConnection(null, null, (frame, _) =>
        {
            sent.Enqueue(frame.ToArray());
            return ValueTask.CompletedTask;
        });
        var sessions = new WorldSessionStore((_, previous, current) => callbacks.Enqueue((previous, current)));
        var staleContextSession = CreateSession();
        sessions.Set(connection, staleContextSession);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: staleContextSession,
            Sessions: sessions);

        var newerAttributes = new Dictionary<string, string>(
            staleContextSession.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["NewestRevisionMarker"] = "wallet-update",
        };
        var newerSession = staleContextSession with
        {
            Character = staleContextSession.Character with
            {
                ConquerPoints = 777,
                Attributes = newerAttributes,
            },
        };
        sessions.Set(connection, newerSession);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgMovementPacket
            {
                Direction = 6,
                Uid = 0,
                Running = MsgMovementMode.Walk,
                Timestamp = 123,
            }.ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var stored));
        var storedSession = Assert.IsType<WorldSession>(stored);
        Assert.Same(newerSession, storedSession);
        Assert.Equal(777, storedSession.Character.ConquerPoints);
        Assert.Equal("wallet-update", storedSession.Character.Attributes["NewestRevisionMarker"]);
        Assert.Equal(staleContextSession.Character.X, storedSession.Character.X);
        Assert.Equal(staleContextSession.Character.Y, storedSession.Character.Y);

        Assert.Collection(
            callbacks,
            callback =>
            {
                Assert.Null(callback.Previous);
                Assert.Same(staleContextSession, callback.Current);
            },
            callback =>
            {
                Assert.Same(staleContextSession, callback.Previous);
                Assert.Same(newerSession, callback.Current);
            });
        Assert.Empty(sent);
        Assert.Contains("WorldServer skipped stale MsgMovement publication", log.ToString());
        Assert.DoesNotContain("WorldServer updated MsgMovement", log.ToString());
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "revision",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "Revision",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
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
                Online: 1,
                SourceFile: "1293600.ini",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StatusFlags"] = BuildStatusFlags(FocusedStatusFlag),
                    ["FocusClientSpellId"] = FocusClientSpellId.ToString(),
                    ["Angle"] = "0",
                },
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[10];
        foreach (var flag in flags)
        {
            values[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', values) + "/";
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(TryReadFrame(bytes, out var frame));
        return frame;
    }

    private static bool TryReadFrame(byte[] bytes, out PacketFrame frame)
    {
        return PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out frame, out _, out _);
    }
}
