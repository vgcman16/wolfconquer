using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterRepeatAutoStatsTests
{
    [Fact]
    public Task NonRebornLevelUpRefreshesStatsAndSpeedBeforeActualDueRepeat()
    {
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        var family = new Monster(910, "AutoStatRepeat", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "10",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var levels = new[]
        {
            new LevelExperience(0, 10, 1, 0, 0, 1, "0 10 1 0 0", new[] { "0", "10", "1", "0", "0" }),
            new LevelExperience(0, 11, 10_000_000, 0, 0, 2, "0 11 10000000 0 0", new[] { "0", "11", "10000000", "0", "0" }),
        };
        var stats = new[] { new ClassLevelStat("Trojan", 11, 200, 40, 30, 0,
            "fixture://Trojan.ini", 1, "11", "200,40,30,0", "11=200,40,30,0", new[] { "200", "40", "30", "0" }) };
        var catalog = LegacyItemWorldDataFixture.Create([], [], classLevelStats: stats, monsters: new[] { family }, levelExperience: levels);
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 10, reborn: 0,
            strength: 100, agility: 10, mapId: 1011, x: 430, y: 380, minHitPoints: 500) with
        {
            Vitality = 20, Spirit = 0, AttributePoints = 12, MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, PlayerItemSnapshot.Empty), catalog, options, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://auto-stat-repeat/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var beforeSpeed));
            Assert.NotNull(beforeSpeed);
            var files = f.Data.Files(); string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var original = Encoding.UTF8.GetString(files[key]);
            var request = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
            f.ActorFrames.Clear(); await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, request.ToFrameBytes());
            var firstDamage = PhysicalDamage(f.ActorFrames, monster.Uid);
            Assert.True(firstDamage > 0);
            long firstXp = Math.Max(10, firstDamage) - 1L;
            var after = f.Current();
            Assert.Equal(11L, after.Character.Level); Assert.Equal(firstXp, after.Character.Experience);
            Assert.Equal(200L, after.Character.Strength); Assert.Equal(40L, after.Character.Agility);
            Assert.Equal(30L, after.Character.Vitality); Assert.Equal(0L, after.Character.Spirit);
            Assert.Equal((ushort)200, binding.Player.Strength); Assert.Equal((ushort)40, binding.Player.Agility);
            Assert.Equal((ushort)30, binding.Player.Vitality); Assert.Equal(checked((ulong)firstXp), binding.Player.Experience);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(after, out var maximum));
            // Class1000: 200*3 + 40*3 + 30*24, with no gear or other HP rows.
            Assert.Equal(1440U, maximum); Assert.Equal(1440L, after.Character.MinHitPoints); Assert.Equal(1440, binding.Player.HitPoints);
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var afterSpeed));
            Assert.NotNull(afterSpeed); Assert.NotSame(beforeSpeed, afterSpeed);
            Assert.Equal((ushort)0, afterSpeed.Right); Assert.Equal((ushort)0, afterSpeed.Left); Assert.Equal((ushort)0, afterSpeed.Ring);
            Assert.True(binding.Client.OnAutoAttack); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var first));
            Assert.True(first.Hitpoints < monster.Hitpoints && first.Hitpoints > 0);
            string expected = Replace(original, "Level=10", "Level=11");
            expected = Replace(expected, "Strength=100", "Strength=200");
            expected = Replace(expected, "Agility=10", "Agility=40");
            expected = Replace(expected, "Vitaliti=20", "Vitaliti=30");
            expected = Replace(expected, "MinHitPoints=500", "MinHitPoints=1440");
            expected = Replace(expected, "Experience=0", "Experience=" + firstXp.ToString(CultureInfo.InvariantCulture));
            files[key] = Encoding.UTF8.GetBytes(expected); f.Data.AssertFiles(files);
            int count = f.ActorFrames.Count;
            // Rebuilt Agility40 shortens the real 700ms baseline to680ms.
            clock.Advance(679); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.Equal(count, f.ActorFrames.Count);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var notDue)); Assert.Equal(first.Hitpoints, notDue.Hitpoints);
            clock.Advance(1); await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var second));
            Assert.True(second.Hitpoints < first.Hitpoints && second.Hitpoints > 0);
            int secondDamage = PhysicalDamage(f.ActorFrames.Skip(count), monster.Uid); Assert.True(secondDamage > 0);
            long secondXp = firstXp + Math.Max(10, secondDamage);
            Assert.Equal(secondXp, f.Current().Character.Experience); Assert.Equal(checked((ulong)secondXp), binding.Player.Experience);
            Assert.Equal((ushort)200, binding.Player.Strength); Assert.Equal((ushort)40, binding.Player.Agility);
            Assert.True(binding.Client.OnAutoAttack); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            expected = Replace(expected, "Experience=" + firstXp.ToString(CultureInfo.InvariantCulture),
                "Experience=" + secondXp.ToString(CultureInfo.InvariantCulture));
            files[key] = Encoding.UTF8.GetBytes(expected); f.Data.AssertFiles(files);
        });
    }

    private static int PhysicalDamage(IEnumerable<byte[]> frames, uint monsterUid)
    {
        var hits = frames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Where(frame => MsgAttackPacket.TryParse(frame, out var packet) && packet.Query.AttackType == MsgAttackType.Physical && packet.Query.OpponentUid == monsterUid)
            .Select(frame => { Assert.True(MsgAttackPacket.TryParse(frame, out var packet)); return packet.Query.Damage; }).ToArray();
        return Assert.Single(hits);
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
