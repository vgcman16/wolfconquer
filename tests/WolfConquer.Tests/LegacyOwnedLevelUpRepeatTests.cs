using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedLevelUpRepeatTests
{
    [Fact]
    public Task RealMonsterHitLevelsThenRealDueRepeatHitsSameLivingBirth()
    {
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        var family = new Monster(910, "LevelRepeat", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(actor, PlayerItemSnapshot.Empty), catalog, options, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-level-repeat/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var request = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
            f.ActorFrames.Clear();
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, request.ToFrameBytes());
            Assert.Equal(140L, f.Current().Character.Level); Assert.Equal((ushort)140, binding.Player.Level);
            Assert.Equal(15L, f.Current().Character.AttributePoints);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(f.Current(), out var maximum));
            Assert.Equal(11028U, maximum); Assert.Equal(11028L, f.Current().Character.MinHitPoints);
            Assert.True(binding.Client.OnAutoAttack); Assert.NotNull(binding.Client.AutoAttack);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var first));
            Assert.True(first.Hitpoints < monster.Hitpoints && first.Hitpoints > 0);
            int afterFirst = f.ActorFrames.Count;
            clock.Advance(700);
            await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var second));
            Assert.True(second.Hitpoints < first.Hitpoints && second.Hitpoints > 0);
            Assert.True(f.ActorFrames.Count > afterFirst);
            Assert.True(binding.Client.OnAutoAttack); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var disk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(f.Current().AccountId);
            Assert.NotNull(disk); Assert.Equal(140L, disk.Level); Assert.Equal(15L, disk.AttributePoints);
            Assert.Equal(11028L, disk.MinHitPoints);
            // Real registry replacement keeps the UID (and even reuses the
            // same input object) but creates a distinct monster state/birth.
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            int beforeReplacementDue = f.ActorFrames.Count;
            clock.Advance(700);
            await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var replacement));
            Assert.Equal(monster.MaxLife, replacement.Hitpoints);
            Assert.Equal(beforeReplacementDue, f.ActorFrames.Count);
            Assert.False(binding.Client.OnAutoAttack);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        });
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
