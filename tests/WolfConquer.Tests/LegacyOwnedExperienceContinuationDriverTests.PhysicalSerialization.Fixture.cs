using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    private const uint EarnedActor = OwnedEquipmentBehaviorFixture.Actor, EarnedPeer = OwnedEquipmentBehaviorFixture.Peer;
    private sealed class EarnedPhysicalClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance() => Interlocked.Add(ref ticks, TimeSpan.FromSeconds(5).Ticks);
    }

    private static Task RunEarnedPhysicalSerialization(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance,
        EarnedPhysicalClock, Task> body)
    {
        var family = new Monster(910, "EarnedPhysicalContinuation", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "137",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        static LevelExperience Threshold(int level, int amount) => new(0, level, amount, 0, 0, 1,
            $"0 {level} {amount} 0 0", ["0", level.ToString(CultureInfo.InvariantCulture), amount.ToString(CultureInfo.InvariantCulture), "0", "0"]);
        var catalog = LegacyItemWorldDataFixture.Create(new ItemType[] { OwnedEquipmentBehaviorFixture.Item(410009) }, [], monsters: new Monster[] { family },
            levelExperience: new LevelExperience[] { Threshold(137, 1), Threshold(138, 1), Threshold(139, 1_000_000_000) });
        static OwnedEquipmentBehaviorInput Input(uint uid, int level, int x, int hp, uint itemUid)
        {
            var character = OwnedEquipmentBehaviorFixture.Character(uid, "EarnedPhysical" + uid, classCode: 7051,
                level: level, reborn: 1, strength: 100, agility: 0, mapId: 1000, x: x, y: 584, minHitPoints: hp) with
            {
                MinMana = 0, MaxMana = 0,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "77", ["PkMode"] = "0", ["Action"] = "0", ["Stamina"] = "100",
                    ["UserExpRate"] = "1", ["MaxMana"] = "0", ["ExpBallUsed"] = "0",
                },
            };
            var items = new PlayerItemSnapshot([], [], [], [new() { Uid = itemUid, ItemId = 410009, Position = 4,
                Durability = 100, MaximumDurability = 100, StackSize = 1 }], []);
            return new(character, items, Proficiencies: new PlayerProficiencySnapshot([new(410, 5, 0, 100, 1_600_000)]));
        }
        var clock = new EarnedPhysicalClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(EarnedActor, 137, 830, 5000, 5_337_721), catalog, options, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1000, family.Id, 831, 584, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1000, family.Name, "fixture://earned-physical-continuation/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            f.ActorFrames.Clear(); f.Frames(EarnedPeer).Clear();
            await body(f, monster, clock);
        }, Input(EarnedPeer, 139, 831, 2000, 5_337_722));
    }

    private static byte[] EarnedPhysicalRequest(bool pendingActor) => new MsgAttackPacket(new MsgAttackQuery(
        Uid: pendingActor ? EarnedActor : EarnedPeer, OpponentUid: pendingActor ? EarnedPeer : EarnedActor,
        X: pendingActor ? (ushort)831 : (ushort)830, Y: 584, AttackType: MsgAttackType.Physical,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();
}
