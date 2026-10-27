using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPoisonedRuntimeExpiryTests
{
    private const int PoisonedFlag = 1;
    private const string PoisonLevelAttribute = "PoisonLevel";
    private static readonly string[] PoisonedExpiresAtUtcAttributes =
    [
        "LegacyPoisonedExpiresAtUtc",
        "LegacyPoisonExpiresAtUtc",
        "LegacyToxicPoisonExpiresAtUtc",
        "LegacyToxicFogPoisonExpiresAtUtc",
    ];

    [Theory]
    [InlineData("LegacyPoisonedExpiresAtUtc")]
    [InlineData("LegacyPoisonExpiresAtUtc")]
    [InlineData("LegacyToxicPoisonExpiresAtUtc")]
    [InlineData("LegacyToxicFogPoisonExpiresAtUtc")]
    public async Task RuntimeLoopExpiresPoisonedFlagAndClearsPoisonAttributes(string expiresAtAttribute)
    {
        var log = new StringWriter();
        var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var clock = new PoisonExpiryTimeProvider(now.AddSeconds(10));
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                RuntimeTickInterval,
                clock),
            log: log);
        var session = CreatePoisonedSession(now.AddSeconds(-1), expiresAtAttribute);
        SetHandlerSession(handler, connection, session);
        Task[] workers = [];
        Exception? failure = null;

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);
            workers = handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.Equal(2, workers.Length);
            Assert.All(workers, worker => Assert.False(worker.IsCompleted));
            await clock.WaitForTimerCountAsync(2).WaitAsync(WorkerBound);
            Assert.Equal(2, clock.TimerCount);
            Assert.Same(session, AssertHandlerSession(handler, connection));
            Assert.True(HasLegacyStatusFlag(session.Character.Attributes, PoisonedFlag));
            Assert.False(HasPoisonClearedStatusUpdate(sent));

            // Start registers the real player delay first and global delay second.
            // Fire only the global delay; its next registration joins the whole tick,
            // including expiry publication and logging, without wall-clock polling.
            clock.FireGlobalTick();
            await clock.WaitForTimerCountAsync(3).WaitAsync(WorkerBound);
            Assert.Equal(3, clock.TimerCount);

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, PoisonedFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey(PoisonLevelAttribute));
            foreach (var attribute in PoisonedExpiresAtUtcAttributes)
            {
                Assert.False(expiredSession.Character.Attributes.ContainsKey(attribute));
            }

            Assert.True(
                HasPoisonClearedStatusUpdate(sent),
                "Expected runtime poison expiry to send a StatusFlag vector with Poisoned cleared.");

            Assert.Contains("WorldServer expired legacy Poisoned flags", log.ToString());
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await FinishPoisonRuntimeAsync(handler, cancel, log, clock, workers, failure);
    }

    private static WorldSession CreatePoisonedSession(DateTimeOffset expiresAt, string expiresAtAttribute)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(PoisonedFlag),
            [PoisonLevelAttribute] = "4",
            [expiresAtAttribute] = expiresAt.ToString("O", CultureInfo.InvariantCulture),
        };
        var character = new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "Poisoned",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
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
            SourceFile: "fixture://legacy-poisoned-runtime-expiry",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());

        return new WorldSession(
            AccountId: 1,
            Username: "poisoned",
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
                lock (sent)
                {
                    sent.Add(frame.ToArray());
                }

                return ValueTask.CompletedTask;
            });
    }

    private static void SetHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        GetHandlerSessions(handler).Set(connection, session);
    }

    private static void StartHandlerRuntimeUpdateLoop(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "StartLegacyRuntimeUpdateLoop",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(
            handler,
            [connection, session.Character, session.PlayerItems, session.PlayerSpells, cancellationToken]);
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        Assert.True(GetHandlerSessions(handler).TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var updates = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static bool HasPoisonClearedStatusUpdate(List<byte[]> sent)
    {
        byte[][] snapshot;
        lock (sent)
        {
            snapshot = sent.ToArray();
        }

        return ParseMsgUpdates(snapshot).Any(update => update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is not null &&
            !IsLegacyStatusFlagSet(entry.Values, PoisonedFlag)));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
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
