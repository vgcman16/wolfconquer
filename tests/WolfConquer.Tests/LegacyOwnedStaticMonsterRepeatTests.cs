using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedStaticMonsterRepeatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StaticHitLevelsAndRepeatsButRetiredBirthCannotRepeat(bool replaceSpawnMetadata)
    {
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        var family = new Monster(910, "StaticRepeat", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var spawnFields = new Dictionary<string, string> { ["OwnedFixtureMarker"] = "original" };
        var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
            1011, family.Name, "fixture://owned-static-repeat/910.ini", spawnFields);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family },
            levelExperience: new[] { threshold }, monsterSpawns: new[] { spawn });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "0", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1",
              ["LegacyMonsterDeathConquerPointRatePercent"] = "0" },
        };
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, PlayerItemSnapshot.Empty), catalog, options, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var entry = Assert.Single(LegacyStaticMonsterMaterializer.Enumerate(catalog, catalog.MonsterSpawnsByMapId[1011]));
            Assert.Same(spawn, entry.Spawn); Assert.Same(family, entry.MonsterFamily);
            var monster = entry.Instance;
            Assert.Equal(0U, monster.DynamicId);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var born));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(born, family, out _));
            Assert.False(states.TryGetDynamicMonsterByUid(monster.MapId, monster.DynamicId, monster.Uid, out _, out _));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files();
            var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string expected = Encoding.UTF8.GetString(files[key]);
            expected = Replace(expected, "Level=139", "Level=140");
            expected = Replace(expected, "Atributes=12", "Atributes=15");
            expected = Replace(expected, "MinHitPoints=5000", "MinHitPoints=11028");
            files[key] = Encoding.UTF8.GetBytes(expected);
            var request = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
            f.ActorFrames.Clear(); await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, request.ToFrameBytes());
            Assert.Equal(140L, f.Current().Character.Level); Assert.Equal((ushort)140, binding.Player.Level);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(f.Current(), out var maximum));
            Assert.Equal(11028U, maximum); Assert.Equal(11028L, f.Current().Character.MinHitPoints);
            Assert.True(binding.Client.OnAutoAttack); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            f.Data.AssertFiles(files);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var first));
            Assert.True(first.Hitpoints < monster.Hitpoints && first.Hitpoints > 0);
            clock.Advance(700); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var second));
            Assert.True(second.Hitpoints < first.Hitpoints && second.Hitpoints > 0);
            Assert.True(binding.Client.OnAutoAttack);
            int count = f.ActorFrames.Count;
            uint expectedHp;
            if (replaceSpawnMetadata)
            {
                spawnFields["OwnedFixtureMarker"] = "changed";
                expectedHp = second.Hitpoints;
            }
            else
            {
                var deathAt = clock.GetUtcNow();
                Assert.True(states.ApplyDamage(monster, monster.MaxLife, deathAt).Killed);
                Assert.True(states.TryConsumeDeathRemovalInstance(monster, deathAt.AddDays(2), out _));
                Assert.True(states.TryConsumeRespawnedInstance(monster, deathAt.AddDays(2), out var respawned));
                Assert.Equal(monster.Uid, respawned.Uid); Assert.Equal(monster.MaxLife, respawned.Hitpoints);
                expectedHp = monster.MaxLife;
            }
            clock.Advance(700); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var current));
            Assert.Equal(expectedHp, current.Hitpoints); Assert.Equal(count, f.ActorFrames.Count);
            Assert.False(binding.Client.OnAutoAttack); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            f.Data.AssertFiles(files);
        });
    }

    private static string Replace(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n"; Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }

    private sealed class AttackClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(int milliseconds) => Interlocked.Add(ref ticks, TimeSpan.FromMilliseconds(milliseconds).Ticks);
    }
}
