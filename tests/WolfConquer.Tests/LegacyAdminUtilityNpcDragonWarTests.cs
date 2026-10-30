using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdminUtilityNpcDragonWarTests
{
    private const uint StuffNpcId = 77_839;
    private const uint Stuff12NpcId = 22_224;
    private const byte OpenDragonWarOptionId = 201;

    [Theory]
    [InlineData(StuffNpcId)]
    [InlineData(Stuff12NpcId)]
    public async Task DisabledAdminRootDoesNotExposeDragonWarOrOtherAdminOptions(uint npcId)
    {
        await AssertDisabledAsync(npcId, option: 0, LegacyDragonWarProcess.Dead);
    }

    [Theory]
    [InlineData(StuffNpcId, LegacyDragonWarProcess.Dead)]
    [InlineData(Stuff12NpcId, LegacyDragonWarProcess.Dead)]
    [InlineData(StuffNpcId, LegacyDragonWarProcess.Idle)]
    [InlineData(Stuff12NpcId, LegacyDragonWarProcess.Idle)]
    [InlineData(StuffNpcId, LegacyDragonWarProcess.Alive)]
    [InlineData(Stuff12NpcId, LegacyDragonWarProcess.Alive)]
    public async Task DisabledAdminOption201CannotOpenResetOrInviteToDragonWar(uint npcId, LegacyDragonWarProcess process)
    {
        await AssertDisabledAsync(npcId, OpenDragonWarOptionId, process);
    }

    private static async Task AssertDisabledAsync(uint npcId, byte option, LegacyDragonWarProcess process)
    {
        using var log = new StringWriter();
        var gmSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var disconnects = 0;
        var gmConnection = CreateSendingConnection(gmSent, 9958, () => disconnects++);
        var peerConnection = CreateSendingConnection(peerSent, 9959, () => disconnects++);
        var sessions = new WorldSessionStore();
        var gmSession = CreateSession(1_293_600, "DragonWarAdmin[GM][PM]");
        var peerSession = CreateSession(1_293_601, "DragonWarGuest");
        foreach (var key in new[] { "ProjectManager", "GM", "IsGM", "GameMaster", "IsGameMaster", "Admin", "IsAdmin", "Privileged" })
            ((Dictionary<string, string>)gmSession.Character.Attributes)[key] = "true";
        ((Dictionary<string, string>)gmSession.Character.Attributes)["Role"] = "Admin";
        sessions.Set(gmConnection, gmSession);
        sessions.Set(peerConnection, peerSession);
        var gmAttributes = gmSession.Character.Attributes.ToArray();
        var peerAttributes = peerSession.Character.Attributes.ToArray();
        var now = new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero);
        var states = CreateState(process, now);
        var control = CreateState(process, now);

        Assert.True(await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(gmConnection, log, Session: gmSession,
                Sessions: sessions, DragonWarStates: states), gmSession, npcId, option, CancellationToken.None));

        Assert.Equal(process, states.Process);
        Assert.True(sessions.TryGet(gmConnection, out var gmAfter));
        Assert.True(sessions.TryGet(peerConnection, out var peerAfter));
        Assert.Same(gmSession, gmAfter);
        Assert.Same(peerSession, peerAfter);
        Assert.Equal(gmAttributes, gmAfter!.Character.Attributes.ToArray());
        Assert.Equal(peerAttributes, peerAfter!.Character.Attributes.ToArray());
        Assert.False(gmAfter.Character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
        Assert.False(peerAfter.Character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
        Assert.Empty(gmSent);
        Assert.Empty(peerSent);
        Assert.Equal(0, disconnects);
        Assert.Contains("reason=ownedGmTransactionRequired", log.ToString());
        // Exercise the real existing event timer after refusal against an
        // independently initialized same-time state; no update is published.
        foreach (var seconds in new[] { 11, 61, 362 })
        {
            var expected = control.CreateDueRuntimeUpdates(now.AddSeconds(seconds), new[] { gmSession, peerSession });
            var actual = states.CreateDueRuntimeUpdates(now.AddSeconds(seconds), new[] { gmSession, peerSession });
            if (process == LegacyDragonWarProcess.Idle && seconds == 11) Assert.NotEmpty(expected.Updates);
            Assert.Equal(expected.Updates.Count, actual.Updates.Count);
            for (var index = 0; index < expected.Updates.Count; index++)
                Assert.Equal(expected.Updates[index].FrameBytes, actual.Updates[index].FrameBytes);
            Assert.Empty(expected.SessionUpdates);
            Assert.Empty(actual.SessionUpdates);
            Assert.Equal(control.Process, states.Process);
        }
    }

    private static LegacyDragonWarRuntimeStateStore CreateState(LegacyDragonWarProcess process, DateTimeOffset now)
    {
        var state = new LegacyDragonWarRuntimeStateStore();
        if (process != LegacyDragonWarProcess.Dead) Assert.NotNull(state.Open(now));
        if (process == LegacyDragonWarProcess.Alive) state.SetProcess(process);
        return state;
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

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port, Action disconnect)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }, _ => { disconnect(); return ValueTask.CompletedTask; });
    }
}
