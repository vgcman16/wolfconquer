using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private sealed class RosterClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    }
    private sealed record Case(OwnedEquipmentBehaviorFixture Fixture, TeamSessionStateStore Teams,
        MonsterRuntimeStateStore Monsters, LegacyMonsterInstance[] Targets, RosterClock Clock, bool Threshold)
    {
        internal string SpellPath => Path.Combine(Fixture.Data.Spells, Peer + ".bin");
    }

    private static Task Run(ushort spell, bool threshold, bool secondMonster, Func<Case, Task> exercise)
    {
        var input = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spell);
        var thresholds = new[]
        {
            new LevelExperience(0, 10, threshold ? 1 : 1_000_000, 0, 0, 1,
                threshold ? "0 10 1 0 0" : "0 10 1000000 0 0",
                new[] { "0", "10", threshold ? "1" : "1000000", "0", "0" }),
            new LevelExperience(0, 11, 1_000_000, 0, 0, 2, "0 11 1000000 0 0",
                new[] { "0", "11", "1000000", "0", "0" }),
        };
        var stats = new[] { new ClassLevelStat("Taoist", 11, 0, 0, 20, 20,
            "fixture://outside-roster/Taoist.ini", 1, "11", "0,0,20,20", "11=0,0,20,20",
            new[] { "0", "0", "20", "20" }) };
        var catalog = LegacyItemWorldDataFixture.Create(Array.Empty<ItemType>(), [], classLevelStats: stats,
            monsters: new[] { input.Family }, magic: input.Catalog.Magic.ToArray(), levelExperience: thresholds,
            combatGear: NativeCombatGearProcFixture.CatalogRows());
        var recipient = input.Peer with
        {
            Character = input.Peer.Character with { X = 830, Y = 594 }, OmitSpellsFile = true,
        };
        var clock = new RosterClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(input.Actor, catalog, options, async f =>
        {
            var actor = f.Current(); var peer = f.Current(Peer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(Assert.IsType<LegacyNativeMyCombatGear>(binding.Client.CombatGear).IsAdmissionOriginCurrent(binding, actor));
            Assert.True(f.NativeCombatGear!.IsCurrentFor(catalog));
            Assert.Equal(201U, Assert.Single(new CombatGearSnapshotLoader(f.Data.CombatGear).LoadByCharacterUid(Actor).Items).ItemID);
            Assert.True(new PlayerSpellSnapshotLoader(f.Data.Spells).LoadByCharacterUid(Actor).ContainsSpell(spell));
            Assert.Equal(30L, peer.Character.MinHitPoints); Assert.Equal(594L, peer.Character.Y);
            Assert.Equal(10L, Math.Max(Math.Abs(actor.Character.X - peer.Character.X), Math.Abs(actor.Character.Y - peer.Character.Y)));
            Assert.Empty(peer.PlayerSpells.Spells); Assert.False(File.Exists(Path.Combine(f.Data.Spells, Peer + ".bin")));
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.InviteRequest, Peer).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            var teams = Field<TeamSessionStateStore>(f.Handler, "_teamStates");
            var roster = teams.GetConnections(f.Connection(Actor));
            Assert.Equal(2, roster.Count); Assert.Contains(f.Connection(Peer), roster);
            Assert.Equal(teams.GetTeamUid(f.Connection(Actor)), teams.GetTeamUid(f.Connection(Peer)));
            var monsters = Field<MonsterRuntimeStateStore>(f.Handler, "_monsterStates");
            var targets = Enumerable.Range(0, secondMonster ? 2 : 1).Select(index =>
            {
                var spawn = new MonsterSpawn(91_870 + index, 1_018, input.Family.Id, 831 + index, 584,
                    1, 1, 1, 10, 1, 0, 0, 0, 0, 1_018, input.Family.Name, "fixture://outside-roster/910.ini",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
                var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, input.Family, monsters.NextLegacyDynamicMonsterUid()));
                Assert.Same(monster, monsters.RegisterDynamicMonster(monster, input.Family));
                Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, input.Family, out _));
                Assert.Equal(500U, monster.Hitpoints);
                return monster;
            }).ToArray();
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await exercise(new(f, teams, monsters, targets, clock, threshold));
        }, recipient);
    }

    private static T Field<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    private static LegacyOrdinarySpellBinding Binding(Case c, uint uid) =>
        Assert.IsType<LegacyOrdinarySpellBinding>(c.Fixture.Current(uid).NativeGameClient!.OrdinarySpellBinding);
    private static byte[] Physical(Case c) => new MsgAttackPacket(new MsgAttackQuery(Actor, c.Targets[0].Uid,
        c.Targets[0].X, c.Targets[0].Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes();
    private static string Log(Case c)
    {
        string log = c.Fixture.Log.ToString(); return log.Length <= 12_000 ? log : log[^12_000..];
    }
}
