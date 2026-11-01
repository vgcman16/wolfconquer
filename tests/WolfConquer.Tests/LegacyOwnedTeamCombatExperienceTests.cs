using System.Reflection;
using System.Text;
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
        var files = f.Data.Files();
        foreach (uint uid in new[] { Actor, Peer, Third })
        {
            string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
            var text = Encoding.UTF8.GetString(files[path]);
            text = ReplaceLine(text, "Level=139", "Level=140");
            text = ReplaceLine(text, "Atributes=12", "Atributes=15");
            // Class 1000, strength 1000, agility 36 and vitality 447.
            text = ReplaceLine(text, "MinHitPoints=5000", "MinHitPoints=13836");
            if (uid == Actor) text = ReplaceLine(text, "ConquerPoints=639778904", "ConquerPoints=639778906");
            files[path] = Encoding.UTF8.GetBytes(text);
        }
        await HitAsync(f, monster);
        Assert.Equal(140, f.Current(Peer).Character.Level);
        Assert.Equal(140, f.Current(Third).Character.Level);
        Assert.Equal((ushort)140, peerBinding.Player.Level);
        Assert.Equal((ushort)140, thirdBinding.Player.Level);
        Assert.Equal(0L, f.Current(Peer).Character.MinMana); Assert.Equal(0L, f.Current(Third).Character.MinMana);
        Assert.Equal(0u, peerBinding.Player.Mana); Assert.Equal(0u, thirdBinding.Player.Mana);
        Assert.Equal(15, f.Current(Peer).Character.AttributePoints);
        Assert.Equal(15, f.Current(Third).Character.AttributePoints);
        Assert.Equal(140, new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(Peer)!.Level);
        Assert.Equal(140, new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(Third)!.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(thirdBinding));
        var actorAfter = f.Current(Actor);
        var actorDisk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(Actor);
        Assert.True(actorAfter.Character.Level == 140 && actorDisk?.Level == 140,
            $"Killer XP owner did not level: session={actorAfter.Character.Level}, " +
            $"disk={actorDisk?.Level}, xp={actorAfter.Character.Experience}; log:\n{f.Log}");
        Assert.Equal(639778906L, actorAfter.Character.ConquerPoints);
        Assert.Equal(639778906U, Binding(f, Actor).Player.ConquerPoints);
        f.Data.AssertFiles(files);
        AssertLevelSequence(f.Frames(Peer), Peer);
        AssertLevelSequence(f.Frames(Third), Third);
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
        var laterFrames = f.Frames(Third).Skip(thirdFramesAtChange).ToArray();
        // Peer query/restore finishes before the killer's own XP/query/restore.
        // Each zero-add, query clamp and restore fans out team HP.
        Assert.Equal(6, laterFrames.Length);
        var updates = laterFrames.Select(bytes =>
        {
            var frame = OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
            Assert.True(LegacyTeamMemberInfoPacket.TryParse(frame, out var packet),
                $"Unexpected post-rejoin opcode {frame.Header.Opcode}; log:\n{f.Log}");
            Assert.Equal(LegacyTeamMemberInfoAction.AddMember, packet.Action);
            Assert.Equal((byte)1, packet.Count); Assert.Equal((ushort)1, packet.Unknown);
            var entry = Assert.Single(packet.Entries);
            Assert.Equal(13836U, entry.MaxHitpoints);
            return (entry.Uid, entry.Name, entry.MinHitpoints);
        }).ToArray();
        Assert.Equal(new (uint, string, uint)[]
        {
            (Peer, "TeamPeer", 5000), (Peer, "TeamPeer", 5000), (Peer, "TeamPeer", 13836),
            (Actor, "TeamLeader", 5000), (Actor, "TeamLeader", 5000), (Actor, "TeamLeader", 13836),
        }, updates);
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
            { MinMana = 0, MaxMana = 0, Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1",
                  ["LegacyMonsterDeathConquerPointRatePercent"] = "100", ["LegacyMonsterDeathConquerPointRateRoll"] = "0",
                  ["LegacyMonsterDeathConquerPointRoll"] = "2" } },
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
    private static string ReplaceLine(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n";
        Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }

    private static void AssertLevelSequence(List<byte[]> frames, uint uid)
    {
        byte[] Update(params (LegacyMsgUpdateDataType Type, ulong Value)[] values) =>
            new LegacyMsgUpdatePacket(uid, values.Select(value => new LegacyMsgUpdateEntry(value.Type, value.Value)).ToArray()).ToFrameBytes();
        int Find(byte[] expected) => frames.FindIndex(bytes => bytes.AsSpan().SequenceEqual(expected));
        int level = Find(Update((LegacyMsgUpdateDataType.Level, 140)));
        int points = Find(Update((LegacyMsgUpdateDataType.Atributes, 15)));
        int status = frames.FindIndex(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
        int refine = frames.FindIndex(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyUserTotalRefineLevPacket.Opcode);
        int restore = Find(Update((LegacyMsgUpdateDataType.MaxHitpoints, 13836), (LegacyMsgUpdateDataType.Hitpoints, 13836)));
        int experience = Find(Update((LegacyMsgUpdateDataType.Experience, 0)));
        Assert.True(level >= 0 && level < points && points < status && status < refine && refine < restore && restore < experience);
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frames[status]), out var query));
        Assert.Equal(13836U, query.MaxHP); Assert.Equal(0U, query.MaxMana);
    }
    private static Task HitAsync(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster) =>
        f.SendAsync(Actor, new MsgAttackPacket(new MsgAttackQuery(Actor, monster.Uid, monster.X, monster.Y,
            MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes());
}
