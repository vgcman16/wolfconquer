using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarAutoOpenRuntimeTests
{
    private const string DragonWarInvitationText = "DragonWar[SS/FB]";
    private const string DragonWarInvitationCenterText = " " + DragonWarInvitationText;

    [Fact]
    public void SchedulerOnlyConsumesSourceOpenSlotOnce()
    {
        var scheduler = new LegacyDragonWarOpenScheduler();
        var due = CreateLocalDragonWarOpenSlot();

        Assert.False(scheduler.TryConsumeDueOpen(due.AddSeconds(-1)));
        Assert.False(scheduler.TryConsumeDueOpen(due.AddSeconds(1)));
        Assert.True(scheduler.TryConsumeDueOpen(due));
        Assert.False(scheduler.TryConsumeDueOpen(due));
        Assert.True(scheduler.TryConsumeDueOpen(due.AddHours(1)));
    }

    [Fact]
    public async Task RuntimeTickAtSourceSlotOpensDragonWarAndInvitesEveryActiveSessionOnce()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetSessions(handler);
        var gmSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var gmConnection = CreateSendingConnection(gmSent, 9958);
        var peerConnection = CreateSendingConnection(peerSent, 9959);
        sessions.Set(gmConnection, CreateSession(1_293_600, "DragonWarAdmin[GM]"));
        sessions.Set(peerConnection, CreateSession(1_293_601, "DragonWarGuest"));

        await InvokeDragonWarRuntimeTickAsync(handler, CreateLocalDragonWarOpenSlot());

        Assert.Equal(LegacyDragonWarProcess.Idle, GetDragonWarStates(handler).Process);
        AssertDragonWarInvitationSent(gmSent);
        AssertDragonWarInvitationSent(peerSent);
        AssertPendingDragonWarInvitation(sessions, gmConnection);
        AssertPendingDragonWarInvitation(sessions, peerConnection);
        Assert.Contains("automatically opened legacy DragonWar runtime", log.ToString());
        Assert.Contains("promptedSessions=2", log.ToString());

        var gmCount = gmSent.Count;
        var peerCount = peerSent.Count;
        await InvokeDragonWarRuntimeTickAsync(handler, CreateLocalDragonWarOpenSlot());

        Assert.Equal(gmCount, gmSent.Count);
        Assert.Equal(peerCount, peerSent.Count);
    }

    [Fact]
    public async Task RuntimeTickOutsideSourceSlotDoesNotOpenDragonWar()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var sessions = GetSessions(handler);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent, 9960);
        sessions.Set(connection, CreateSession(1_293_600, "DragonWarAdmin[GM]"));

        await InvokeDragonWarRuntimeTickAsync(handler, CreateLocalDragonWarOpenSlot().AddSeconds(1));

        Assert.Equal(LegacyDragonWarProcess.Dead, GetDragonWarStates(handler).Process);
        Assert.Empty(sent);
        Assert.DoesNotContain("DragonWar runtime", log.ToString());
    }

    private static async Task InvokeDragonWarRuntimeTickAsync(WorldServerFrameHandler handler, DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyDragonWarRuntimeUpdatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(handler, [now, CancellationToken.None]);
        Assert.NotNull(result);
        await (ValueTask)result;
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

    private static DateTimeOffset CreateLocalDragonWarOpenSlot()
    {
        var local = new DateTime(
            2026,
            6,
            28,
            12,
            LegacyDragonWarOpenScheduler.OpenMinute,
            LegacyDragonWarOpenScheduler.OpenSecond,
            DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static void AssertDragonWarInvitationSent(IReadOnlyList<byte[]> sent)
    {
        Assert.Contains(sent, IsDragonWarSystemCenter);
        Assert.Contains(sent, IsDragonWarInvitationMessageBox);
    }

    private static bool IsDragonWarSystemCenter(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Content.Contains(DragonWarInvitationCenterText);
    }

    private static bool IsDragonWarInvitationMessageBox(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
            reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            reply.Text == DragonWarInvitationText;
    }

    private static void AssertPendingDragonWarInvitation(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(
            LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
            updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.PendingAttribute]);
        Assert.Equal("1002", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute]);
        Assert.Equal("319", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationXAttribute]);
        Assert.Equal("256", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationYAttribute]);
        Assert.Equal("0", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute]);
        Assert.Equal("60", updated.Character.Attributes[LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute]);
    }

    private static WorldSession CreateSession(long uid, string name)
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
                Level: 100,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
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
}
