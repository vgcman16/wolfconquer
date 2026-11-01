using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlNonPlayerTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private const uint Damage = 1_050, InitialHp = 100_000;

    private sealed class NonPlayerClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref ticks, amount.Ticks);
    }

    private static Task Run(bool sobTarget, Func<NonPlayerFixture, Task> body, ushort manaCost = 0)
    {
        var input = LegacyMagicAttackRuntimeTests.OwnedDragonhowlNonPlayerInputs(sobTarget, manaCost);
        var clock = new NonPlayerClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(input.Actor, input.World, options, async owner =>
        {
            var monsters = ReadField<MonsterRuntimeStateStore>(owner.Handler, "_monsterStates");
            var sobs = ReadField<SobNpcRuntimeStateStore>(owner.Handler, "_sobNpcStates");
            LegacyMonsterInstance? birth = null;
            if (input.Sob is { } sob)
                Assert.Equal((int)InitialHp, sobs.GetVisible(sob).Life); // Actual existing registration API.
            else
            {
                var spawn = new MonsterSpawn(31_749, 1_000, input.Family.Id, 831, 584, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                    1_000, input.Family.Name, "fixture://owned-dragonhowl-nonplayer/monster.ini",
                    new Dictionary<string, string>());
                birth = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, input.Family, monsters.NextLegacyDynamicMonsterUid()));
                Assert.Same(birth, monsters.RegisterDynamicMonster(birth, input.Family));
                Assert.True(owner.Store.TryReadMonsterPhysicalRange(birth, input.Family, out _));
            }
            var f = new NonPlayerFixture(owner, input, clock, monsters, sobs, birth);
            var actor = owner.Current();
            var fields = LegacyPlayerStatusCalculator.CalculateFields(actor.Character, actor.PlayerItems, input.World, InitialHp, 0);
            Assert.Equal(300U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
            Assert.Equal(301U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
            Assert.Equal(0U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
            Assert.Equal(100_000, actor.Character.MinHitPoints);
            Assert.True(owner.NativeCombatGear!.IsCurrentFor(input.World));
            owner.ActorFrames.Clear();
            await body(f);
        });
    }

    private static T ReadField<T>(object owner, string name) => Assert.IsType<T>(owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(owner));

    private sealed class NonPlayerFixture(OwnedEquipmentBehaviorFixture owner, OwnedDragonhowlNonPlayerInput input,
        NonPlayerClock clock, MonsterRuntimeStateStore monsters, SobNpcRuntimeStateStore sobs, LegacyMonsterInstance? birth)
    {
        internal readonly OwnedEquipmentBehaviorFixture Owner = owner;
        internal readonly OwnedDragonhowlNonPlayerInput Input = input;
        internal readonly NonPlayerClock Clock = clock;
        internal readonly MonsterRuntimeStateStore Monsters = monsters;
        internal readonly SobNpcRuntimeStateStore Sobs = sobs;
        internal readonly LegacyMonsterInstance? Birth = birth;
        internal uint TargetUid => Birth?.Uid ?? checked((uint)Input.Sob!.Id);
        internal WorldSession Current => Owner.Current();
        internal LegacyOrdinarySpellBinding Binding => Current.NativeGameClient!.OrdinarySpellBinding!;
        internal LegacyMonsterInstance Monster()
        {
            Assert.NotNull(Birth);
            Assert.True(Monsters.TryGetVisibleInstance(Birth, Clock.GetUtcNow(), out var current));
            return current;
        }
        internal uint Hp() => Birth is null ? checked((uint)Sobs.GetVisible(Input.Sob!).Life) : Monster().Hitpoints;
        internal MsgAttackPacket Request() => new(new MsgAttackQuery(Uid: Actor, OpponentUid: TargetUid,
            X: 831, Y: 584, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
            ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0));
        internal Task Attack() => Owner.SendAsync(Actor, Request().ToFrameBytes());
        internal async Task Repeat()
        {
            Clock.Advance(TimeSpan.FromMilliseconds(700));
            await Owner.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
        }
    }
}
