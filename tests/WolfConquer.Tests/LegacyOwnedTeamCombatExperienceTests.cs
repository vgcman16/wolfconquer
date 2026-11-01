using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedTeamCombatExperienceTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private const uint Peer = OwnedEquipmentBehaviorFixture.Peer;
    private const uint Third = Peer + 1;

    [Fact]
    public Task LethalHitAwardsActualAdmittedTeammates() => RunAsync(async (f, monster, teams, holders) =>
    {
        var peerBinding = Binding(f, Peer);
        var thirdBinding = Binding(f, Third);
        await HitAsync(f, monster);
        Assert.Equal(140, f.Current(Peer).Character.Level);
        Assert.Equal(140, f.Current(Third).Character.Level);
        Assert.Equal((ushort)140, peerBinding.Player.Level);
        Assert.Equal((ushort)140, thirdBinding.Player.Level);
        Assert.Equal(15, f.Current(Peer).Character.AttributePoints);
        Assert.Equal(15, f.Current(Third).Character.AttributePoints);
        Assert.Equal(140, new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(Peer)!.Level);
        Assert.Equal(140, new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(Third)!.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(thirdBinding));
        Assert.NotEmpty(f.Frames(Peer));
        Assert.NotEmpty(f.Frames(Third));
    });

    [Fact]
    public Task ExitAndRejoinDuringFirstAwardCannotAuthorizeStaleSecondAward() => RunAsync(async (f, monster, teams, holders) =>
    {
        var thirdBefore = f.Current(Third);
        var thirdBytes = File.ReadAllBytes(f.CharacterPath(Third));
        var thirdBinding = Binding(f, Third);
        Assert.True(holders.TryCaptureClient(f.Connection(Actor), out var leader));
        Assert.True(holders.TryCaptureClient(f.Connection(Third), out var third));
        bool changed = false;
        int thirdFramesAtChange = -1;
        f.BeforeSend = (uid, _) =>
        {
            if (uid != Peer || changed || Binding(f, Peer).Player.Level != 140) return;
            changed = true;
            thirdFramesAtChange = f.Frames(Third).Count;
            teams.ExitTeam(f.Connection(Third));
            Assert.True(teams.TryAddNativeMember(leader!, third!, requireLeader: true));
        };
        try { await HitAsync(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.True(changed);
        Assert.Equal(140, f.Current(Peer).Character.Level);
        Assert.Same(thirdBefore, f.Current(Third));
        Assert.Equal(thirdBytes, File.ReadAllBytes(f.CharacterPath(Third)));
        Assert.Equal((ushort)139, thirdBinding.Player.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(thirdBinding));
        Assert.Equal(thirdFramesAtChange, f.Frames(Third).Count);
    });

    private static Task RunAsync(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, TeamSessionStateStore,
        LegacyTournamentWatcherHolders, Task> body)
    {
        var family = new Monster(910, "TeamExperience", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "100", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        OwnedEquipmentBehaviorInput Player(uint uid, string name) => new(
            OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 1000, level: 139, reborn: 1,
                strength: 1000, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
            { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" } },
            PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(Player(Actor, "TeamLeader"), catalog, async f =>
        {
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            await f.SendAsync(Third, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            var teams = Field<TeamSessionStateStore>(f, "_teamStates");
            var holders = Field<LegacyTournamentWatcherHolders>(f, "_tournamentWatcherHolders");
            Assert.Equal(new[] { f.Connection(Actor), f.Connection(Peer), f.Connection(Third) }, teams.GetConnections(f.Connection(Actor)));
            var states = Field<MonsterRuntimeStateStore>(f, "_monsterStates");
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-team-xp/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            f.ActorFrames.Clear(); f.Frames(Peer).Clear(); f.Frames(Third).Clear();
            await body(f, monster, teams, holders);
        }, Player(Peer, "TeamPeer"), Player(Third, "TeamThird"));
    }

    private static T Field<T>(OwnedEquipmentBehaviorFixture f, string name) where T : class =>
        Assert.IsType<T>(typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid) =>
        Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
    private static Task HitAsync(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster) =>
        f.SendAsync(Actor, new MsgAttackPacket(new MsgAttackQuery(Actor, monster.Uid, monster.X, monster.Y,
            MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes());
}
