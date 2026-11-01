using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlAwardSequenceTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;

    private sealed class SequenceClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed record Sequence(OwnedEquipmentBehaviorFixture Fixture, LegacyMonsterInstance[] Monsters,
        MonsterRuntimeStateStore Store, SequenceClock Clock, bool Multiple, bool MissingSpells)
    {
        internal string SpellPath => Path.Combine(Fixture.Data.Spells, Peer + ".bin");
    }

    private static Task RunSequence(ushort spellId, bool multiple, bool missingSpells,
        Func<Sequence, Task> exercise, bool omitSecondStats = false)
    {
        // Reuse only declared saved input construction; real native19 loading,
        // ordinary admission, Team packets and monster births below own work.
        var input = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spellId);
        var thresholds = new[]
        {
            Threshold(10, multiple ? 1 : 1_000_000), Threshold(11, 10), Threshold(12, 1_000_000),
        };
        var stats = new List<ClassLevelStat> { Stats(11, 20) };
        if (!omitSecondStats) stats.Add(Stats(12, 30));
        var catalog = LegacyItemWorldDataFixture.Create(Array.Empty<ItemType>(), [],
            classLevelStats: stats.ToArray(), monsters: new[] { input.Family },
            magic: input.Catalog.Magic.ToArray(), levelExperience: thresholds,
            combatGear: NativeCombatGearProcFixture.CatalogRows());
        var peer = input.Peer with
        {
            Character = input.Peer.Character with { X = multiple ? 833 : 832 },
            OmitSpellsFile = missingSpells,
        };
        var clock = new SequenceClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(input.Actor, catalog, options, async f =>
        {
            var actor = f.Current(); var target = f.Current(Peer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(Assert.IsType<LegacyNativeMyCombatGear>(actor.NativeGameClient.CombatGear)
                .IsAdmissionOriginCurrent(binding, actor));
            Assert.True(f.NativeCombatGear!.IsCurrentFor(catalog));
            Assert.Equal(201U, Assert.Single(new CombatGearSnapshotLoader(f.Data.CombatGear)
                .LoadByCharacterUid(Actor).Items).ItemID);
            Assert.True(new PlayerSpellSnapshotLoader(f.Data.Spells).LoadByCharacterUid(Actor).ContainsSpell(spellId));
            Assert.Equal(51_000L, actor.Character.MinHitPoints);
            Assert.Equal(30L, target.Character.MinHitPoints);
            Assert.Empty(target.PlayerSpells.Spells);
            var path = Path.Combine(f.Data.Spells, Peer + ".bin");
            if (missingSpells) Assert.False(File.Exists(path));
            else Assert.Equal(new byte[] { 0, 0, 0, 0 }, File.ReadAllBytes(path));

            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.InviteRequest, Peer).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            var teams = Field<TeamSessionStateStore>(f.Handler, "_teamStates");
            var roster = teams.GetConnections(f.Connection(Actor));
            Assert.Equal(2, roster.Count);
            Assert.Contains(f.Connection(Actor), roster); Assert.Contains(f.Connection(Peer), roster);
            Assert.Equal(teams.GetTeamUid(f.Connection(Actor)), teams.GetTeamUid(f.Connection(Peer)));
            Assert.Equal("0", actor.Character.Attributes["PkMode"]);
            var store = Field<MonsterRuntimeStateStore>(f.Handler, "_monsterStates");
            var monsters = Enumerable.Range(0, multiple ? 2 : 1).Select(index =>
            {
                var spawn = new MonsterSpawn(91_850 + index, 1_018, input.Family.Id, 831 + index, 584,
                    1, 1, 1, 10, 1, 0, 0, 0, 0, 1_018, input.Family.Name, "fixture://award-sequence/910.ini",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
                var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, input.Family, store.NextLegacyDynamicMonsterUid()));
                Assert.Same(monster, store.RegisterDynamicMonster(monster, input.Family));
                Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, input.Family, out _));
                Assert.Equal(500U, monster.Hitpoints); Assert.Equal((byte)10, monster.Level);
                return monster;
            }).ToArray();
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await exercise(new(f, monsters, store, clock, multiple, missingSpells));
        }, peer);
    }

    private static LevelExperience Threshold(int level, long experience) => new(0, level, experience, 0, 0, level,
        $"0 {level} {experience} 0 0", new[] { "0", level.ToString(), experience.ToString(), "0", "0" });
    private static ClassLevelStat Stats(int level, int value) => new("Taoist", level, 0, 0, value, value,
        "fixture://award-sequence/Taoist.ini", level, level.ToString(), $"0,0,{value},{value}",
        $"{level}=0,0,{value},{value}", new[] { "0", "0", value.ToString(), value.ToString() });
    private static T Field<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
    private static object CompletedQueryIdentity(LegacyOrdinarySpellBinding binding)
    {
        // Read-only observation of the real query-issued association. Never
        // construct, register, replace or pass this object to an owner API.
        object Read(object table, object key)
        {
            object?[] arguments = [key, null];
            Assert.True((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)!);
            return Assert.IsAssignableFrom<object>(arguments[1]);
        }
        var type = typeof(LegacyNativeMagicQueryClient);
        var entries = type.GetField("Entries", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var completions = type.GetField("CurrentMaximumCompletions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        return Read(completions, Read(entries, binding.NativeIdentity));
    }
    private static byte[] Request(Sequence s) => new MsgAttackPacket(new MsgAttackQuery(Uid: Actor,
        OpponentUid: s.Monsters[0].Uid, X: s.Monsters[0].X, Y: s.Monsters[0].Y, AttackType: MsgAttackType.Physical,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();
}
