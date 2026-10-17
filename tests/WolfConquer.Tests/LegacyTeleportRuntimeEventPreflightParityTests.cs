using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportRuntimeEventPreflightParityTests
{
    private static readonly DateTimeOffset DragonWarOpenedAt =
        new(2026, 6, 28, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FreezeWarOpenedAt =
        new(2026, 6, 20, 12, 55, 0, TimeSpan.Zero);

    [Fact]
    public async Task DragonWarReviveDeniesForeignParticipantBeforeReviveAndTeleportsOwnServerControl()
    {
        var handler = new WorldServerFrameHandler(snapshotLoader: null);
        var sessions = GetSessions(handler);
        var dragonWarStates = GetDragonWarStates(handler);
        Assert.NotNull(dragonWarStates.Open(DragonWarOpenedAt));

        var foreignSent = new List<byte[]>();
        var ownServerSent = new List<byte[]>();
        var foreignConnection = CreateConnection(foreignSent, 9_961);
        var ownServerConnection = CreateConnection(ownServerSent, 9_962);
        var foreignJoined = JoinDragonWar(
            dragonWarStates,
            CreateSession(1_293_700, "ForeignDragon", onMyOwnServer: false));
        var ownServerJoined = JoinDragonWar(
            dragonWarStates,
            CreateSession(1_293_701, "OwnDragon", onMyOwnServer: true));
        var tickAt = DragonWarOpenedAt.AddSeconds(20);
        var foreignDead = MarkDead(foreignJoined, tickAt.AddSeconds(-5), x: 1, y: 1);
        var ownServerDead = MarkDead(ownServerJoined, tickAt.AddSeconds(-5), x: 2, y: 2);
        AssertForeignWithoutTeleportAllowances(foreignDead);

        sessions.Set(foreignConnection, foreignDead);
        sessions.Set(ownServerConnection, ownServerDead);
        dragonWarStates.SetProcess(LegacyDragonWarProcess.Alive);

        await InvokeDragonWarRuntimeTickAsync(handler, tickAt);

        var foreignAfter = GetSession(sessions, foreignConnection);
        Assert.Equal(foreignDead.Character.MapId, foreignAfter.Character.MapId);
        Assert.Equal(foreignDead.Character.X, foreignAfter.Character.X);
        Assert.Equal(foreignDead.Character.Y, foreignAfter.Character.Y);
        Assert.Equal(foreignDead.Character.PreviousMapId, foreignAfter.Character.PreviousMapId);
        Assert.Equal(foreignDead.Character.PreviousMapX, foreignAfter.Character.PreviousMapX);
        Assert.Equal(foreignDead.Character.PreviousMapY, foreignAfter.Character.PreviousMapY);
        Assert.Equal(0, foreignAfter.Character.MinHitPoints);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            foreignAfter.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(
            LegacyPlayerDeathState.HasStatusFlag(
                foreignDead.Character.Attributes,
                LegacyPlayerDeathState.GhostStatusFlag),
            LegacyPlayerDeathState.HasStatusFlag(
                foreignAfter.Character.Attributes,
                LegacyPlayerDeathState.GhostStatusFlag));
        Assert.DoesNotContain(
            foreignSent,
            frame => IsTeleportForUid(frame, foreignAfter.Character.Uid));
        Assert.DoesNotContain(
            foreignSent,
            frame => IsStatusUpdateForUid(frame, foreignAfter.Character.Uid));

        var ownServerAfter = GetSession(sessions, ownServerConnection);
        Assert.Equal(LegacyDragonWarRuntimeStateStore.DragonWarMapId, ownServerAfter.Character.MapId);
        Assert.NotEqual(ownServerDead.Character.X, ownServerAfter.Character.X);
        Assert.NotEqual(ownServerDead.Character.Y, ownServerAfter.Character.Y);
        Assert.True(ownServerAfter.Character.MinHitPoints > 0);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            ownServerAfter.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            ownServerAfter.Character.Attributes,
            LegacyPlayerDeathState.GhostStatusFlag));
        Assert.Contains(
            ownServerSent,
            frame => IsTeleportForUid(frame, ownServerAfter.Character.Uid));
    }

    [Fact]
    public async Task FreezeWarRoundResetDeniesForeignMoveAfterUnfreezeAndTeleportsOwnServerControl()
    {
        var handler = new WorldServerFrameHandler(snapshotLoader: null);
        var sessions = GetSessions(handler);
        var freezeWarStates = GetFreezeWarStates(handler);
        Assert.NotNull(freezeWarStates.Open(FreezeWarOpenedAt));

        var foreignSent = new List<byte[]>();
        var ownServerSent = new List<byte[]>();
        var foreignConnection = CreateConnection(foreignSent, 9_963);
        var ownServerConnection = CreateConnection(ownServerSent, 9_964);
        Assert.True(freezeWarStates.TryJoin(
            CreateSession(1_293_702, "ForeignFreeze", onMyOwnServer: false),
            out var foreignJoined,
            out var foreignTeam));
        Assert.Equal(LegacyFreezeWarTeamType.Red, foreignTeam);
        Assert.True(freezeWarStates.TryJoin(
            CreateSession(1_293_703, "OwnFreeze", onMyOwnServer: true),
            out var ownServerJoined,
            out var ownServerTeam));
        Assert.Equal(LegacyFreezeWarTeamType.Blue, ownServerTeam);
        sessions.Set(foreignConnection, foreignJoined);
        sessions.Set(ownServerConnection, ownServerJoined);

        await InvokeFreezeWarRuntimeTickAsync(handler, FreezeWarOpenedAt.AddSeconds(61));
        SetFreezeWarParticipant(sessions, foreignConnection, x: 160, y: 160, frozen: false);
        SetFreezeWarParticipant(sessions, ownServerConnection, x: 170, y: 170, frozen: true);
        await InvokeFreezeWarRuntimeTickAsync(handler, FreezeWarOpenedAt.AddSeconds(62));

        var foreignBeforeReset = GetSession(sessions, foreignConnection);
        var ownServerBeforeReset = GetSession(sessions, ownServerConnection);
        AssertForeignWithoutTeleportAllowances(foreignBeforeReset);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            foreignBeforeReset.Character.Attributes,
            LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(
            ownServerBeforeReset.Character.Attributes,
            LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));
        foreignSent.Clear();
        ownServerSent.Clear();

        await InvokeFreezeWarRuntimeTickAsync(handler, FreezeWarOpenedAt.AddSeconds(68));

        var foreignAfter = GetSession(sessions, foreignConnection);
        Assert.Equal(foreignBeforeReset.Character.MapId, foreignAfter.Character.MapId);
        Assert.Equal(foreignBeforeReset.Character.X, foreignAfter.Character.X);
        Assert.Equal(foreignBeforeReset.Character.Y, foreignAfter.Character.Y);
        Assert.Equal(foreignBeforeReset.Character.PreviousMapId, foreignAfter.Character.PreviousMapId);
        Assert.Equal(foreignBeforeReset.Character.PreviousMapX, foreignAfter.Character.PreviousMapX);
        Assert.Equal(foreignBeforeReset.Character.PreviousMapY, foreignAfter.Character.PreviousMapY);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            foreignAfter.Character.Attributes,
            LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));
        Assert.DoesNotContain(
            foreignSent,
            frame => IsTeleportForUid(frame, foreignAfter.Character.Uid));
        Assert.Contains(
            foreignSent,
            frame => IsStatusUpdateWithoutFlag(
                frame,
                foreignAfter.Character.Uid,
                LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));

        var ownServerAfter = GetSession(sessions, ownServerConnection);
        Assert.Equal(LegacyFreezeWarRuntimeStateStore.FreezeWarMapId, ownServerAfter.Character.MapId);
        Assert.Equal(153, ownServerAfter.Character.X);
        Assert.Equal(105, ownServerAfter.Character.Y);
        Assert.Equal(ownServerBeforeReset.Character.PreviousMapId, ownServerAfter.Character.PreviousMapId);
        Assert.Equal(ownServerBeforeReset.Character.PreviousMapX, ownServerAfter.Character.PreviousMapX);
        Assert.Equal(ownServerBeforeReset.Character.PreviousMapY, ownServerAfter.Character.PreviousMapY);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            ownServerAfter.Character.Attributes,
            LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));
        Assert.Contains(
            ownServerSent,
            frame => IsTeleportForUid(frame, ownServerAfter.Character.Uid));
        Assert.Contains(
            ownServerSent,
            frame => IsStatusUpdateWithoutFlag(
                frame,
                ownServerAfter.Character.Uid,
                LegacyFreezeWarRuntimeStateStore.FreezeStatusFlag));
    }

    private static WorldSession JoinDragonWar(
        LegacyDragonWarRuntimeStateStore states,
        WorldSession session)
    {
        Assert.True(states.TryJoin(session, out var joined));
        return joined;
    }

    private static WorldSession MarkDead(
        WorldSession session,
        DateTimeOffset deadAt,
        ushort x,
        ushort y)
    {
        var positioned = session.Character with { X = x, Y = y };
        var death = LegacyPlayerDeathState.ApplyDeath(positioned, deadAt, x, y);
        return session with { Character = death.Character };
    }

    private static void SetFreezeWarParticipant(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        long x,
        long y,
        bool frozen)
    {
        var session = GetSession(sessions, connection);
        var character = LegacyFreezeWarRuntimeStateStore.MarkAliveAndUnfreeze(
            session.Character with { X = x, Y = y });
        if (frozen)
        {
            character = LegacyFreezeWarRuntimeStateStore.MarkRoundFrozen(character);
        }

        sessions.Set(connection, session with { Character = character });
    }

    private static void AssertForeignWithoutTeleportAllowances(WorldSession session)
    {
        Assert.Equal("false", session.Character.Attributes["OnMyOwnServer"]);
        foreach (var attribute in new[]
                 {
                     "SetLocationType",
                     "InQualifier",
                     "Qualifier",
                     "InQualifierMatch",
                     "InTeamQualifier",
                     "TeamQualifier",
                     "InTeamQualifierMatch",
                     "Watching",
                     "IsWatching",
                     "ArenaWatching",
                     "RuntimeWatching",
                 })
        {
            Assert.False(session.Character.Attributes.ContainsKey(attribute));
        }
    }

    private static WorldSession CreateSession(long uid, string name, bool onMyOwnServer)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OnMyOwnServer"] = onMyOwnServer ? "true" : "false",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static LegacyDragonWarRuntimeStateStore GetDragonWarStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_dragonWarStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyDragonWarRuntimeStateStore>(field.GetValue(handler));
    }

    private static LegacyFreezeWarRuntimeStateStore GetFreezeWarStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_freezeWarStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyFreezeWarRuntimeStateStore>(field.GetValue(handler));
    }

    private static WorldSession GetSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static async ValueTask InvokeDragonWarRuntimeTickAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyDragonWarRuntimeUpdatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(handler, [now, CancellationToken.None]);
        await Assert.IsType<ValueTask>(result).ConfigureAwait(false);
    }

    private static async ValueTask InvokeFreezeWarRuntimeTickAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyFreezeWarRuntimeUpdatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(handler, [now, TimeZoneInfo.Utc, CancellationToken.None]);
        await Assert.IsType<ValueTask>(result).ConfigureAwait(false);
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, port),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static bool IsTeleportForUid(byte[] bytes, long uid)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Type == MsgActionType.Teleport &&
            packet.ObjId == checked((uint)uid);
    }

    private static bool IsStatusUpdateForUid(byte[] bytes, long uid)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == checked((uint)uid) &&
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.StatusFlag);
    }

    private static bool IsStatusUpdateWithoutFlag(byte[] bytes, long uid, int flag)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == checked((uint)uid) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.StatusFlag &&
                update.Values is { } values &&
                !HasStatusFlag(values, flag));
    }

    private static bool HasStatusFlag(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
